---
name: ProductionStandards
description: Technical and design standards for production-grade, HIPAA-compliant, and maintainable software development within the MuseumAdmin project.
---

# Production Standards Skill

This skill ensures that all code, architecture, and UI/UX decisions within the `MuseumAdmin` project adhere to high-quality production standards, legal compliance (HIPAA), and extreme usability.

## 1. Production Grade Code
- **Error Handling**: Use robust `try-catch` blocks at service and UI boundaries. Implement fallback UIs and user-friendly error messages.
- **Validation**: All user inputs must be validated on both the client (Blazor) and server-side. Use Data Annotations (`[Required]`, `[EmailAddress]`, etc.) consistently.
- **Logging**: Implement structured logging for all critical operations, API interactions, and errors. Ensure to log the request and response payloads for API interactions for debugging.
- **Async/Await**: Always use asynchronous patterns for I/O bound operations (API calls, DB access) to maintain UI responsiveness.
- **Dependency Injection**: Use DI for services and configurations to ensure testability and modularity.

## 2. Scalable Data & Performance
- **Streaming Deserialization**: For large API payloads (e.g., >1000 records), always use `JsonSerializer.DeserializeAsync` with `Stream` rather than reading the entire content as a `string`. This prevents high memory overhead and "Connection reset" errors.
- **Resilient API Patterns**: Implement retry logic with exponential backoff for transient network errors (Connection Reset, Timeouts) in service layers.
- **Progressive UI Rendering**: When displaying large datasets, render an initial subset (e.g., first 10-20 items) immediately and load the remainder progressively or in the background to maintain UI responsiveness.
- **Proactive Pre-fetching**: Use background tasks to pre-fetch critical data (like catalogs or configuration) before the user interaction requires it.
- **SharedLogic: Avoid duplicating data transformation logic across multiple components. Centralize it in a shared service or utility class to ensure consistency and maintainability.

## 3. HIPAA Compliance & Security
- **PHI Protection**: NEVER log Sensitive Personal Information or Protected Health Information (PHI).
- **Data Masking**: Use masking techniques (e.g., `J*** D***` for names, `***-**-1234` for identifiers) when displaying data in general lists unless explicitly requested by a privileged user.
- **Audit Logging**: Every access, modification, or deletion of patient/user data must be logged with:
    - User ID
    - Timestamp
    - Action performed
    - Affected Record ID
- **Session Security**: Implement inactivity timeouts and secure session management.

## 4. UI/UX: Usability First
- **Primary Goal: Usability**: This application serves non-technical administrators and healthcare providers. Clarity and ease of use are paramount.
- **Aesthetics**: Maintain a modern, premium look with clean layouts, consistent spacing, and a professional color palette. Ensure uniformity across the app.
- **Interactive Elements**:
    - Use clear labels and ARIA attributes for accessibility.
    - Action buttons should be prominent and their purpose obvious.
    - Provide immediate feedback for user actions (loaders, success ticks, error toasts).
- **Visual Cues**: Use status indicators (e.g., green ticks for saved, amber dots for pending) to reduce cognitive load.
- **Responsive Design**: Ensure full usability across desktop and tablet devices used in healthcare settings.

## 5. Maintainability & Documentation
- **XML Documentation**: Every public class, method, and property in C# must have `<summary>` XML tags explaining its purpose, parameters, and return values.
- **Logic Commenting**: Explain "Why" something is being done, not just "What" the code does, especially for complex business logic.
- **Consistent Naming**: Follow standard C# and .NET naming conventions (PascalCase for methods/properties, camelCase for local variables).
- **Modular Architecture**: Keep components small and focused. Refactor logic from `.razor` files into dedicated Services or ViewModels.

## Usage
Apply these standards to every prompt response. If a requested feature conflicts with these standards, proactively suggest a compliant/standardized alternative.
