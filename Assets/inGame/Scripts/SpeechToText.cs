using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Meta.WitAi.Dictation;
using Meta.WitAi;
using Meta.WitAi.Events;
using System.Text.RegularExpressions;

public class SpeechToText : MonoBehaviour
{
    public DictationService dictationService;
    public TMP_InputField textInputField;

    [SerializeField] private bool appendText = true; // Set to false if you want to replace text instead of append
    [SerializeField] private bool resetTextOnPinch = true; // New option to control text reset behavior

    [Header("Hand Tracking")]
    [SerializeField] private OVRHand hand; // Reference to OVRHand component (usually right hand)
    [SerializeField] private float pinchThresholdActivation = 0.8f; // Threshold for pinch detection (0-1)
    [SerializeField] private float pinchThresholdDeactivation = 0.6f; // Threshold for unpinch detection (0-1)

    private bool isPinching = false;
    private bool dictationActive = false;
    private string lastFullTranscription = "";
    private string currentTextBase = "";
    private bool isProcessingFullTranscription = false;

    private void Start()
    {
        // Validate references
        if (dictationService == null)
        {
            Debug.LogError("Dictation Service reference is missing!");
            enabled = false;
            return;
        }

        if (textInputField == null)
        {
            Debug.LogError("Text Input Field reference is missing!");
            enabled = false;
            return;
        }

        if (hand == null)
        {
            Debug.LogError("OVRHand reference is missing!");
            enabled = false;
            return;
        }

        // Set up the dictation events
        dictationService.DictationEvents.OnFullTranscription.AddListener(OnFullTranscriptionReceived);
        dictationService.DictationEvents.OnPartialTranscription.AddListener(OnPartialTranscriptionReceived);
    }

    private void Update()
    {
        CheckPinchGesture();
    }

    private void CheckPinchGesture()
    {
        // Check if hand tracking is available and hand is tracked
        if (!hand.IsTracked)
            return;

        // Get the pinch strength (between index finger and thumb)
        float pinchStrength = hand.GetFingerPinchStrength(OVRHand.HandFinger.Index);

        // Check for pinch gesture (start dictation)
        if (!isPinching && pinchStrength >= pinchThresholdActivation)
        {
            isPinching = true;
            if (!dictationActive)
            {
                StartDictation();
            }
        }
        // Check for unpinch gesture (stop dictation)
        else if (isPinching && pinchStrength <= pinchThresholdDeactivation)
        {
            isPinching = false;
            if (dictationActive)
            {
                StopDictation();
            }
        }
    }

    private void StartDictation()
    {
        if (dictationService.Active)
            return;

        dictationService.Activate();
        dictationActive = true;
        Debug.Log("Dictation started. Speak now...");

        // Reset text field if the option is enabled
        if (resetTextOnPinch)
        {
            textInputField.text = "";
            currentTextBase = "";
        }
        else if (appendText)
        {
            // If not resetting but appending, capture current text
            currentTextBase = textInputField.text;
        }
        else
        {
            // If replacing text but not resetting, prepare for replacement
            currentTextBase = "";
            textInputField.text = "";
        }
    }

    private void StopDictation()
    {
        if (!dictationService.Active)
            return;

        dictationService.Deactivate();
        dictationActive = false;
        Debug.Log("Dictation stopped.");

        // Clear the temporary fields
        lastFullTranscription = "";
    }

    private void OnPartialTranscriptionReceived(string text)
    {
        // Skip if we're currently processing a full transcription
        if (isProcessingFullTranscription)
            return;

        // Update the text field with current base + partial text
        if (appendText && !resetTextOnPinch) // Only append if we're not resetting on pinch
        {
            string displayText = string.IsNullOrEmpty(currentTextBase)
                ? text
                : currentTextBase + " " + text;

            textInputField.text = displayText;
        }
        else
        {
            textInputField.text = text;
        }
    }

    private void OnFullTranscriptionReceived(string text)
    {
        // Skip empty transcriptions
        if (string.IsNullOrEmpty(text))
            return;

        // Set flag to prevent partial updates during full processing
        isProcessingFullTranscription = true;

        // Check if this transcription is a duplicate of the previous one
        if (text == lastFullTranscription)
        {
            isProcessingFullTranscription = false;
            return;
        }

        lastFullTranscription = text;

        // Handle completed phrase
        if (appendText && !resetTextOnPinch) // Only append if we're not resetting on pinch
        {
            // Make sure we're not adding a duplicate phrase
            string newText = string.IsNullOrEmpty(currentTextBase)
                ? text
                : currentTextBase + " " + text;

            // Use regular expressions to check for repeated phrases at the end
            newText = RemoveRepeatedPhrases(newText);

            textInputField.text = newText;

            // Update the base text for the next dictation
            currentTextBase = newText;
        }
        else
        {
            // Just replace the text
            textInputField.text = text;
            currentTextBase = resetTextOnPinch ? "" : text;
        }

        isProcessingFullTranscription = false;
    }

    private string RemoveRepeatedPhrases(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        // Split the input into words
        string[] words = input.Split(' ');

        if (words.Length <= 3) // Need at least a few words to detect phrases
            return input;

        // Check for repeating phrases of different lengths (from 3 to 6 words)
        for (int phraseLength = 3; phraseLength <= 6 && phraseLength * 2 <= words.Length; phraseLength++)
        {
            // Get the last n words as a potential repeated phrase
            string[] lastPhrase = new string[phraseLength];
            string[] secondLastPhrase = new string[phraseLength];

            for (int i = 0; i < phraseLength; i++)
            {
                lastPhrase[i] = words[words.Length - phraseLength + i];
                secondLastPhrase[i] = words[words.Length - (2 * phraseLength) + i];
            }

            // Check if the phrases are the same
            bool phrasesMatch = true;
            for (int i = 0; i < phraseLength; i++)
            {
                if (!lastPhrase[i].Equals(secondLastPhrase[i], System.StringComparison.OrdinalIgnoreCase))
                {
                    phrasesMatch = false;
                    break;
                }
            }

            // If we found a repeated phrase, remove the last occurrence
            if (phrasesMatch)
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                for (int i = 0; i < words.Length - phraseLength; i++)
                {
                    if (i > 0)
                        sb.Append(" ");
                    sb.Append(words[i]);
                }
                return sb.ToString();
            }
        }

        return input;
    }

    private void OnDestroy()
    {
        // Clean up event listeners
        if (dictationService != null)
        {
            dictationService.DictationEvents.OnFullTranscription.RemoveListener(OnFullTranscriptionReceived);
            dictationService.DictationEvents.OnPartialTranscription.RemoveListener(OnPartialTranscriptionReceived);
        }
    }
}