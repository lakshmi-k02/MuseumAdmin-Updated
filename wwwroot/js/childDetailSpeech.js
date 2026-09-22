(function () {
    const SpeechRecognitionCtor = window.SpeechRecognition || window.webkitSpeechRecognition;
    let recognition = null;
    let dotNetRef = null;
    let activeField = null;
    let baseText = "";
    let dictatedText = "";
    let sessionId = 0;

    function appendSegment(existing, segment) {
        const trimmedSegment = (segment || "").trim();
        if (!trimmedSegment) {
            return existing || "";
        }

        const prefix = existing && !/\s$/.test(existing) ? " " : "";
        return `${existing || ""}${prefix}${trimmedSegment}`.trimStart();
    }

    function pushText(interimText) {
        if (!dotNetRef || !activeField) {
            return;
        }

        const composed = appendSegment(appendSegment(baseText, dictatedText), interimText);
        dotNetRef.invokeMethodAsync("UpdateSpeechText", activeField, composed);
    }

    function updateStatus(isListening, message) {
        if (!dotNetRef || !activeField) {
            return;
        }

        dotNetRef.invokeMethodAsync("UpdateSpeechStatus", activeField, isListening, message || null);
    }

    function cleanup() {
        recognition = null;
        if (!activeField) {
            dotNetRef = null;
        }
    }

    window.childDetailSpeech = {
        isSupported: function () {
            return !!SpeechRecognitionCtor;
        },
        start: function (interopRef, fieldName, currentText) {
            if (!SpeechRecognitionCtor) {
                return false;
            }

            sessionId += 1;
            const currentSession = sessionId;

            if (recognition) {
                recognition.stop();
            }

            dotNetRef = interopRef;
            activeField = fieldName;
            baseText = currentText || "";
            dictatedText = "";

            recognition = new SpeechRecognitionCtor();
            recognition.lang = "en-US";
            recognition.continuous = true;
            recognition.interimResults = true;

            recognition.onstart = function () {
                if (currentSession !== sessionId) {
                    return;
                }
                updateStatus(true, "Listening...");
            };

            recognition.onresult = function (event) {
                if (currentSession !== sessionId) {
                    return;
                }

                let interimText = "";

                for (let i = event.resultIndex; i < event.results.length; i += 1) {
                    const transcript = event.results[i][0] ? event.results[i][0].transcript : "";
                    if (event.results[i].isFinal) {
                        dictatedText = appendSegment(dictatedText, transcript);
                    } else {
                        interimText = appendSegment(interimText, transcript);
                    }
                }

                pushText(interimText);
            };

            recognition.onerror = function (event) {
                if (currentSession !== sessionId) {
                    return;
                }

                const errorMap = {
                    "not-allowed": "Microphone permission was denied.",
                    "service-not-allowed": "Speech recognition service is not allowed.",
                    "no-speech": "No speech detected. Try again.",
                    "audio-capture": "No microphone was detected.",
                    "network": "Speech recognition needs network access."
                };

                updateStatus(false, errorMap[event.error] || "Speech recognition stopped.");
            };

            recognition.onend = function () {
                if (currentSession !== sessionId) {
                    return;
                }

                updateStatus(false, "Tap the mic to dictate.");
                activeField = null;
                cleanup();
            };

            try {
                recognition.start();
            } catch (error) {
                cleanup();
                return false;
            }

            return true;
        },
        stop: function () {
            if (recognition) {
                recognition.stop();
            } else if (dotNetRef && activeField) {
                updateStatus(false, "Tap the mic to dictate.");
                activeField = null;
                cleanup();
            }
        }
    };
})();
