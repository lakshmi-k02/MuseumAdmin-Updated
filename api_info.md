# MuseumAdmin API Documentation

This document provides a comprehensive reference of all backend API endpoints invoked per page and component across the MuseumAdmin system, detailing HTTP methods, parameters/payloads, triggers, and calling services.

---

## Base URLs
* **Standard API Base**: `https://membyapi.azurewebsites.net/memby/`
* **Root API Base**: `https://membyapi.azurewebsites.net/`

---

## 1. Assign Exercise Drawer (`Components/Shared/AssignExerciseDrawer.razor`)

The `AssignExerciseDrawer` manages module and exercise assignments, customizable schedules (RTM/frequency/timing), check-in forms, and notifications per patient/contact.

### 1.1 Load Available Modules & Exercises Catalog
* **Endpoint**: `GET https://membyapi.azurewebsites.net/memby/api/Musium/GetModulesWithMessagesWithMessages?museumId={museumId}`
* **Service Method**: `UserService.GetAvailableModulesAsync(museumId, operatorName)`
* **Parameters**:
  * `museumId` (Query string, integer): Museum ID (defaults to `AppState.MuseumId` or `28`).
* **Trigger**: Drawer opening initialization (`InitializeDrawerForCurrentContactAsync`) and `RetryLoadingModulesAsync`.
* **Purpose**: Fetches all available curriculum modules along with their associated messages/exercises for the museum.

### 1.2 Fetch Missing Module Exercises (Fallback / On-Demand)
* **Endpoint**: `GET https://membyapi.azurewebsites.net/memby/api/Musium/GetByModuleId?moduleId={moduleId}`
* **Service Method**: `UserService.GetModuleExercisesAsync(moduleId, operatorName)`
* **Parameters**:
  * `moduleId` (Query string, integer): Target module ID.
* **Trigger**: Parallel exercise fetch during drawer init if exercises were not pre-populated, or when clicking to expand an unloaded module.
* **Purpose**: Retrieves all exercises/messages for a specific module.

### 1.3 Fetch Contact Active Therapy Assignments
* **Endpoint**: `GET https://membyapi.azurewebsites.net/memby/api/GetAllAssignedByChild/{contactId}`
* **Service Method**: `UserService.GetContactAssignmentsAsync(contactId, operatorName)`
* **Parameters**:
  * `contactId` (Route parameter, integer): The target child/contact ID.
* **Trigger**: Drawer opening if `primaryContact.Assignments == null`.
* **Purpose**: Checks active assignments to determine which modules/exercises are currently assigned.

### 1.4 Get Therapist Programs per Patient
* **Endpoint**: `POST https://membyapi.azurewebsites.net//api/TherapistPrograms/get-programs`
* **Service Method**: `UserService.GetTherapistProgramsAsync(museumId, userId, userType, operatorName)`
* **Request Payload**:
  ```json
  {
    "museumId": 28,
    "assignedBy": {
      "id": 123,
      "type": "USER"
    },
    "assignedTo": {
      "id": 456,
      "type": "CONTACT"
    }
  }
  ```
* **Trigger**: Drawer opening (`InitializeDrawerForCurrentContactAsync`) and after creating/updating/deleting programs.
* **Purpose**: Loads active and draft therapist programs, timing schedules, days of the week, delivery windows, and attached check-in forms.

### 1.5 Create Therapist Program (Schedule & Assign)
* **Endpoint**: `POST https://membyapi.azurewebsites.net/api/TherapistPrograms/create`
* **Invoked Via**: Direct HTTP call in `AssignExerciseDrawer.razor` (`SaveProgramScheduleAsync`)
* **Request Payload**:
  ```json
  {
    "museumId": 28,
    "assignedBy": { "id": 123, "type": "USER" },
    "assignedTo": { "id": 456, "type": "CONTACT" },
    "frequency": { "type": "DAILY", "daysOfWeek": ["MONDAY", "WEDNESDAY"] },
    "timing": {
      "type": "WINDOW",
      "window": "MORNING",
      "preferredTime": "09:00:00",
      "timezone": "America/New_York"
    },
    "target": {
      "type": "EXERCISE",
      "exerciseId": 789,
      "moduleId": 12
    },
    "notifications": {
      "sms": true,
      "push": false,
      "recipients": [ { "type": "USER", "id": 1001, "role": "PRIMARY_CAREGIVER" } ]
    },
    "checkInForm": {
      "formId": "form_xyz",
      "formType": "DEFAULT",
      "timing": "AFTER"
    }
  }
  ```
* **Trigger**: Clicking "Save Schedule" or assigning an exercise/module with custom timing.
* **Purpose**: Creates an active recurring schedule/program for the patient.

### 1.6 Update Therapist Program
* **Endpoint**: `PUT https://membyapi.azurewebsites.net/api/TherapistPrograms/update/{id}`
* **Invoked Via**: Direct HTTP call in `AssignExerciseDrawer.razor` (`SaveProgramScheduleAsync`)
* **Parameters**:
  * `id` (Route parameter, string/guid): ID of the therapist program.
* **Request Payload**: Updated program payload (same structure as create).
* **Trigger**: Modifying schedule days, timing windows, forms, or notification caregivers.

### 1.7 Delete Therapist Program (Unassign / Remove Schedule)
* **Endpoint**: `DELETE https://membyapi.azurewebsites.net/api/TherapistPrograms/delete/{id}`
* **Invoked Via**: Direct HTTP call in `AssignExerciseDrawer.razor` (`DeleteProgramScheduleAsync`)
* **Parameters**:
  * `id` (Route parameter, string/guid): Program ID to remove.
* **Trigger**: Clicking "Remove Schedule" or unassigning a scheduled exercise.

### 1.8 Legacy Assign Therapy
* **Endpoint**: `POST https://membyapi.azurewebsites.net/memby/api/AssignTherapy`
* **Service Method**: `UserService.AssignTherapyAsync(contactId, moduleId, messageId, operatorName)`
* **Request Payload**:
  ```json
  {
    "contactId": 456,
    "moduleId": 12,
    "messageId": 789
  }
  ```
* **Trigger**: Legacy quick-assign fallback when not using full program builder.

### 1.9 Fetch Caregivers for Contact
* **Endpoint**: `GET https://membyapi.azurewebsites.net/memby/api/Musium/GetUsersByMuseumId/{museumId}`
* **Service Method**: `UserService.GetUsersAsync(museumId, operatorName)`
* **Trigger**: `LoadCaregiversForCurrentContactAsync` when configuring timing notifications.
* **Purpose**: Finds the parent/account owner (`UserId`) corresponding to `primaryContact.UserId` and loads all associated caregivers.

### 1.10 Fetch Check-In Forms for Dropdown
* **Endpoints**:
  * Default Forms: `GET https://membyapi.azurewebsites.net/memby/api/admin/check-in-form/default?museumId={museumId}`
  * Custom Forms: `GET https://membyapi.azurewebsites.net/memby/api/therapist/check-in-form/custom?therapistId={therapistId}`
* **Trigger**: `LoadCheckInFormsForDropdownAsync` during drawer initialization.
* **Purpose**: Populates the check-in form selector in the timing/schedule modal.

---

## 2. Children / Patients Page (`Components/Pages/Children.razor`)

The `Children` page lists all patients, their caregiver contacts, assignment statuses, and provides quick assignment actions.

### 2.1 Fetch All Families & Children
* **Endpoint**: `GET https://membyapi.azurewebsites.net/memby/api/Musium/GetUsersByMuseumId/{museumId}`
* **Service Method**: `UserService.GetUsersAsync(museumId, operatorName)`
* **Trigger**: Page initialization (`OnInitializedAsync`, `LoadDataAsync`).
* **Purpose**: Retrieves all family units, parent users, and nested child contacts.

### 2.2 Fetch Contact Assignments
* **Endpoint**: `GET https://membyapi.azurewebsites.net/memby/api/GetAllAssignedByChild/{contactId}`
* **Service Method**: `UserService.GetContactAssignmentsAsync(contactId, operatorName)`
* **Trigger**: Loaded per child during list processing.
* **Purpose**: Determines module and exercise assignment counts per child.

### 2.3 Fetch Completed Modules
* **Endpoint**: `GET https://membyapi.azurewebsites.net/memby/api/Musium/GetCompletedModulesForContact?contactId={contactId}`
* **Service Method**: `UserService.GetCompletedModulesForContactAsync(contactId, operatorName)`
* **Trigger**: When viewing patient progress or completed assignments.
* **Purpose**: Displays completed therapy modules.

### 2.4 Pre-Fetch Available Modules
* **Endpoint**: `GET https://membyapi.azurewebsites.net/memby/api/Musium/GetModulesWithMessagesWithMessages?museumId={museumId}`
* **Service Method**: `UserService.GetAvailableModulesAsync(museumId, operatorName)`
* **Trigger**: Background task on page load to prepare data ahead of opening the assignment drawer.

### 2.5 Fetch Providers / Therapists
* **Endpoint**: `GET https://membyapi.azurewebsites.net/memby/api/GetTherapistsByMuseum?museumId={museumId}`
* **Service Method**: `UserService.GetProvidersAsync(museumId, operatorName)`
* **Trigger**: Initializing filters to filter patients by assigned provider.

### 2.6 Add Child / Contact
* **Endpoint**: `POST https://membyapi.azurewebsites.net/memby/api/AddChild`
* **Service Method**: `UserService.AddChildAsync(model, operatorName)`
* **Request Payload**:
  ```json
  {
    "userId": 1001,
    "contactName": "Child Name",
    "dateOfBirth": "2018-05-15",
    "gender": "Male",
    "notes": "..."
  }
  ```
* **Trigger**: Submitting the "Add Child" modal.

### 2.7 Update Child / Contact
* **Endpoint**: `POST https://membyapi.azurewebsites.net/memby/api/UpdateChildren`
* **Service Method**: `UserService.UpdateChildAsync(model, operatorName)`
* **Request Payload**: Child record with updated profile fields.
* **Trigger**: Submitting the "Edit Child" modal.

### 2.8 Deassign Therapy
* **Endpoint**: `POST https://membyapi.azurewebsites.net/memby/api/DeassignTherapy`
* **Service Method**: `UserService.DeassignTherapyAsync(contactId, moduleId, messageId, operatorName)`
* **Request Payload**:
  ```json
  {
    "contactId": 456,
    "moduleId": 12,
    "messageId": 789
  }
  ```
* **Trigger**: Quick de-assign action from the child's assignment chip or table row.

---

## 3. Child Detail Page (`Components/Pages/ChildDetail.razor`)

Displays comprehensive individual history, profile, caregiver contacts, active therapy, and RTM clinical notes.

### 3.1 Fetch Child & Family Information
* **Endpoint**: `GET https://membyapi.azurewebsites.net/memby/api/Musium/GetUsersByMuseumId/{museumId}`
* **Service Method**: `UserService.GetUsersAsync(museumId, operatorName)`
* **Trigger**: Page initialization using route parameter `Id` (`contactId`).

### 3.2 Fetch Child Assignments
* **Endpoint**: `GET https://membyapi.azurewebsites.net/memby/api/GetAllAssignedByChild/{contactId}`
* **Service Method**: `UserService.GetContactAssignmentsAsync(contactId, operatorName)`
* **Trigger**: Loaded to render active programs and exercises tab.

### 3.3 Fetch Completed Modules History
* **Endpoint**: `GET https://membyapi.azurewebsites.net/memby/api/Musium/GetCompletedModulesForContact?contactId={contactId}`
* **Service Method**: `UserService.GetCompletedModulesForContactAsync(contactId, operatorName)`
* **Trigger**: Loaded to render completed therapy history tab.

### 3.4 Update Child Details
* **Endpoint**: `POST https://membyapi.azurewebsites.net/memby/api/UpdateChildren`
* **Service Method**: `UserService.UpdateChildAsync(model, operatorName)`
* **Trigger**: Saving changes to child demographics, diagnosis, or notes.

---

## 4. Care Check-In Builder (`Components/Pages/CareCheckIn.razor`)

Manages custom clinical check-in questions, rating scales, and forms created by therapists or admins.

### 4.1 Fetch Custom Forms for Therapist
* **Endpoint**: `GET https://membyapi.azurewebsites.net/memby/api/therapist/check-in-form/custom?therapistId={therapistId}`
* **Trigger**: Page load (`LoadFormsAsync`).
* **Purpose**: Retrieves all custom forms created by the logged-in therapist.

### 4.2 Fetch Default Admin Check-In Forms
* **Endpoint**: `GET https://membyapi.azurewebsites.net/memby/api/admin/check-in-form/default?museumId={museumId}`
* **Trigger**: Page load (`LoadFormsAsync`).
* **Purpose**: Retrieves museum-wide standard baseline check-in forms.

### 4.3 Create Custom Check-In Form
* **Endpoint**: `POST https://membyapi.azurewebsites.net/memby/api/therapist/check-in-form/custom`
* **Request Payload**:
  ```json
  {
    "therapistId": 123,
    "title": "Daily Mood & Mobility Check",
    "description": "Evaluate morning energy and stiffness",
    "questions": [
      {
        "id": "q1",
        "text": "How is your child's mood today?",
        "type": "scale",
        "minLabel": "Very Low",
        "maxLabel": "Very Happy"
      }
    ]
  }
  ```
* **Trigger**: Clicking "Publish Form" or "Create Check-In Form".

### 4.4 Update Custom Check-In Form
* **Endpoint**: `PUT https://membyapi.azurewebsites.net/memby/api/therapist/check-in-form/custom/{formId}`
* **Parameters**:
  * `formId` (Route parameter, string): Identifier of the form.
* **Request Payload**: Updated form schema and question list.
* **Trigger**: Saving edits to an existing custom check-in form.

### 4.5 Delete Custom Check-In Form
* **Endpoint**: `DELETE https://membyapi.azurewebsites.net/memby/api/therapist/check-in-form/custom/{formId}`
* **Parameters**:
  * `formId` (Route parameter, string): Form ID.
* **Trigger**: Clicking delete on a custom form card.

---

## 5. Families & Caregivers (`Components/Pages/Users.razor`)

Manages families, primary and secondary caregivers, and account setup.

### 5.1 Load Families / Users
* **Endpoint**: `GET https://membyapi.azurewebsites.net/memby/api/Musium/GetUsersByMuseumId/{museumId}`
* **Service Method**: `UserService.GetUsersAsync(museumId, operatorName)`
* **Trigger**: Page load (`OnInitializedAsync`).

### 5.2 Add New Family
* **Endpoint**: `POST https://membyapi.azurewebsites.net/memby/api/AddFamily`
* **Request Payload**:
  ```json
  {
    "museumId": 28,
    "firstName": "Jane",
    "lastName": "Doe",
    "userEmail": "jane.doe@example.com",
    "phoneNumber": "555-0199",
    "role": "Parent",
    "children": [ ... ]
  }
  ```
* **Trigger**: Submitting the "Add New Family" form.

### 5.3 Update Family Details
* **Endpoint**: `POST https://membyapi.azurewebsites.net/memby/api/Musium/UpdateFamily`
* **Request Payload**: Family model containing primary/secondary caregiver updates.
* **Trigger**: Submitting the edit family modal.

---

## 6. Connect / Exercise & Module Builder (`Components/Pages/Connect.razor`)

Authoring tool for curriculum modules, multimedia exercises, instructions, and video uploads.

### 6.1 Fetch Modules & Exercises
* **Endpoint**: `GET https://membyapi.azurewebsites.net/memby/api/Musium/GetModulesWithMessagesWithMessages?museumId={museumId}`
* **Trigger**: Page load to display the curriculum tree.

### 6.2 Add New Module
* **Endpoint**: `POST https://membyapi.azurewebsites.net/memby/api/Musium/AddModule`
* **Request Payload**:
  ```json
  {
    "museumId": 28,
    "name": "Gross Motor Skills 101",
    "description": "Foundation movement exercises"
  }
  ```
* **Trigger**: Submitting the "Create Module" dialog.

### 6.3 Update Module
* **Endpoint**: `POST https://membyapi.azurewebsites.net/memby/api/Musium/UpdateModule`
* **Request Payload**: Module ID and updated metadata.
* **Trigger**: Saving changes to a module title/description.

### 6.4 Add Exercise / Message
* **Endpoint**: `POST https://membyapi.azurewebsites.net/memby/api/Musium/AddMessage`
* **Request Payload**:
  ```json
  {
    "moduleId": 12,
    "title": "Single Leg Balance",
    "message": "Hold balance for 30 seconds on right leg",
    "mediaUrl": "https://.../video.mp4",
    "isActive": true
  }
  ```
* **Trigger**: Adding a new exercise to a module.

### 6.5 Update Exercise / Message
* **Endpoint**: `POST https://membyapi.azurewebsites.net/memby/api/Musium/UpdateMessage`
* **Trigger**: Saving edits to an exercise.

### 6.6 Upload Exercise Video
* **Endpoint**: `POST https://membyapi.azurewebsites.net/api/videos/uploadVideo`
* **Format**: `multipart/form-data` with video file stream.
* **Trigger**: Uploading video demonstrations for exercises.

---

## 7. Bulk Exercise Import (`Components/Pages/BulkImport.razor`)

Imports complete exercise libraries from Excel (`.xlsx`) files.

* **Endpoints Used**:
  * `POST https://membyapi.azurewebsites.net/memby/api/Musium/AddModule`
  * `POST https://membyapi.azurewebsites.net/memby/api/Musium/AddMessage`
* **Service**: `BulkExerciseImportService.cs`
* **Trigger**: Uploading an Excel exercise catalog and clicking "Import".

---

## 8. RTM Encounter & Clinical Notes (`Components/Shared/RtmNoteEditor.razor`)

Handles Remote Therapeutic Monitoring (RTM) clinical session documentation and billing checks.

### 8.1 Create Clinical Encounter
* **Endpoint**: `POST https://membyapi.azurewebsites.net/memby/api/Encounters/CreateEncounter`
* **Request Payload**:
  ```json
  {
    "contactId": 456,
    "therapistId": 123,
    "encounterDate": "2026-09-06T14:00:00Z",
    "durationMinutes": 20,
    "clinicalNote": "...",
    "cptCodes": ["98975", "98977", "98980"]
  }
  ```
* **Trigger**: Signing and saving an RTM encounter note.

### 8.2 Check Billing & Insurance Eligibility
* **Endpoint**: `POST https://membyapi.azurewebsites.net/memby/api/Billing/Eligibility`
* **Request Payload**: Contact ID and billing codes for reimbursement validation.
* **Trigger**: Automated check when reviewing billing codes in the RTM Note Editor.

---

## 9. Authentication & User Settings (`Components/Pages/Settings.razor`)

Manages user profile, notifications, session security, and credential updates.

### 9.1 Change / Reset User Password
* **Endpoint**: `POST https://membyapi.azurewebsites.net/memby/api/Musium/ChangeMuseumUserPassword`
* **Service Method**: `MuseumAuthService.ChangePasswordAsync(request)`
* **Request Payload**:
  ```json
  {
    "userId": 41,
    "userName": "maura@freshoutlooktherapy.com",
    "currentPassword": "Temp@123",
    "newPassword": "Happy@1234A",
    "confirmPassword": "Happy@1234A"
  }
  ```
* **Trigger**: Submitting the "Change Account Password" form in the Password & Security tab of the Settings page.
* **Response**:
  ```json
  {
    "status": true,
    "statusCode": 200,
    "message": "Password updated successfully."
  }
  ```

