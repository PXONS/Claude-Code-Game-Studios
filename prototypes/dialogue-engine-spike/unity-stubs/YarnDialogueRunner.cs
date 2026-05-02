// PROTOTYPE - NOT FOR PRODUCTION
// Question: Can YarnSpinner's Unity runtime wire cleanly to CharacterStateManager?
// Date: 2026-05-02

using UnityEngine;
using Yarn.Unity;

/// Stub showing how YarnSpinner integrates with Masks' state architecture.
/// YarnSpinner's VariableStorageBehaviour is the key integration seam.
public class YarnDialogueRunner : MonoBehaviour
{
    [SerializeField] private DialogueRunner _dialogueRunner;
    [SerializeField] private MasksVariableStorage _variableStorage;

    private string _characterId;

    public void StartScene(string characterId, string nodeName)
    {
        _characterId = characterId;
        // Variable storage pushes state before the node runs (see MasksVariableStorage).
        _variableStorage.LoadCharacterState(characterId);
        _dialogueRunner.StartDialogue(nodeName);
    }
}

/// YarnSpinner integration point: subclass VariableStorageBehaviour to back
/// Yarn variables with CharacterStateManager and PlayerProfile.
/// This is the canonical YarnSpinner pattern — override GetValue/SetValue.
public class MasksVariableStorage : VariableStorageBehaviour
{
    private CharacterStateManagerStub _stateManager = new CharacterStateManagerStub();
    private PlayerProfileStub _playerProfile = new PlayerProfileStub();
    private string _currentCharacterId;

    public void LoadCharacterState(string characterId)
    {
        _currentCharacterId = characterId;
        // No explicit "push" needed — GetValue is called by Yarn at runtime.
    }

    public override bool TryGetValue<T>(string variableName, out T result)
    {
        object value = variableName switch
        {
            var n when n == $"${_currentCharacterId}_depth_tier"
                => _stateManager.GetDepthTier(_currentCharacterId),
            var n when n == $"${_currentCharacterId}_closed_off"
                => _stateManager.IsClosedOff(_currentCharacterId),
            "$pc_name"          => _playerProfile.DisplayName,
            "$pc_pronoun_sub"   => _playerProfile.PronounSubject,
            _                   => null
        };

        if (value is T typedValue)
        {
            result = typedValue;
            return true;
        }
        result = default;
        return false;
    }

    public override void SetValue(string variableName, string stringValue) =>
        HandleSet(variableName, stringValue);
    public override void SetValue(string variableName, float floatValue) =>
        HandleSet(variableName, floatValue);
    public override void SetValue(string variableName, bool boolValue) =>
        HandleSet(variableName, boolValue);

    private void HandleSet(string variableName, object value)
    {
        // YarnSpinner calls SetValue immediately when <<set>> executes in a node.
        // State writes are real-time — no flush step needed (contrast with Ink).
        if (variableName == $"${_currentCharacterId}_depth_tier" && value is float tier)
            _stateManager.SetDepthTier(_currentCharacterId, (int)tier);
        else if (variableName == $"${_currentCharacterId}_closed_off" && value is bool closed)
            _stateManager.SetClosedOff(_currentCharacterId, closed);
    }

    // YarnSpinner requires these — minimal implementations for the spike.
    public override void Clear() { }
    public override bool Contains(string variableName) => true;
    public override (IEnumerable<string>, IEnumerable<float>, IEnumerable<bool>) GetDebugList() =>
        (System.Array.Empty<string>(), System.Array.Empty<float>(), System.Array.Empty<bool>());
}

// ── Custom command handler for <<notify_state_change>> ───────────────────────
// In YarnSpinner, custom commands are registered on the DialogueRunner.
// This would live in a bootstrapper in production.
public class YarnCommandRegistrar : MonoBehaviour
{
    [SerializeField] private DialogueRunner _dialogueRunner;
    private CharacterStateManagerStub _stateManager = new CharacterStateManagerStub();

    private void Awake()
    {
        // <<notify_state_change characterId fieldName value>>
        // Redundant with SetValue for simple vars — useful for triggering
        // downstream events (audio state change, sprite swap) without
        // embedding game logic in the variable storage layer.
        _dialogueRunner.AddCommandHandler<string, string, string>(
            "notify_state_change",
            (characterId, fieldName, value) =>
            {
                Debug.Log($"[YarnCommand] state change: {characterId}.{fieldName} = {value}");
                // In production: fire an event that AudioSystem, SpriteSystem etc. observe.
            }
        );
    }
}

// ── Minimal stubs ─────────────────────────────────────────────────────────────

public class CharacterStateManagerStub
{
    public int GetDepthTier(string characterId) => 0;
    public bool IsClosedOff(string characterId) => false;
    public void SetDepthTier(string characterId, int tier) { }
    public void SetClosedOff(string characterId, bool value) { }
}

public class PlayerProfileStub
{
    public string DisplayName => "Alex";
    public string PronounSubject => "they";
}
