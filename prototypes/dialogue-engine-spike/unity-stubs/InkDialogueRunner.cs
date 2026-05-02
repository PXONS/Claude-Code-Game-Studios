// PROTOTYPE - NOT FOR PRODUCTION
// Question: Can Ink's Unity runtime wire cleanly to CharacterStateManager?
// Date: 2026-05-02

using Ink.Runtime;
using UnityEngine;

/// Stub showing how Ink integrates with Masks' state architecture.
/// In production this would be fully event-driven; here it's minimal.
public class InkDialogueRunner : MonoBehaviour
{
    [SerializeField] private TextAsset _inkJsonAsset;

    // In production: injected via DI. Here: direct reference for spike clarity.
    private CharacterStateManagerStub _stateManager;
    private PlayerProfileStub _playerProfile;

    private Story _story;
    private string _characterId;

    public void StartScene(string characterId, string knotName)
    {
        _characterId = characterId;
        _story = new Story(_inkJsonAsset.text);

        // Push current state INTO Ink before running.
        // This is the critical integration point: Ink variables mirror C# state.
        _story.variablesState["depth_tier"] = _stateManager.GetDepthTier(characterId);
        _story.variablesState["closed_off"] = _stateManager.IsClosedOff(characterId);
        _story.variablesState["pc_name"] = _playerProfile.DisplayName;
        _story.variablesState["pc_pronoun_sub"] = _playerProfile.PronounSubject;

        _story.ChoosePathString(knotName);
        Advance();
    }

    public void Advance()
    {
        while (_story.canContinue)
        {
            string line = _story.Continue();
            Debug.Log(line); // In production: send to DialogueDisplaySystem
        }

        if (_story.currentChoices.Count > 0)
        {
            // In production: send choices to UI system for display.
            for (int i = 0; i < _story.currentChoices.Count; i++)
                Debug.Log($"[{i}] {_story.currentChoices[i].text}");
        }
        else
        {
            // Scene ended — pull state back OUT of Ink.
            FlushStateToManager();
        }
    }

    public void SelectChoice(int index)
    {
        _story.ChooseChoiceIndex(index);
        Advance();

        // If the scene ends after a choice, flush state.
        if (!_story.canContinue && _story.currentChoices.Count == 0)
            FlushStateToManager();
    }

    private void FlushStateToManager()
    {
        // CRITICAL: Ink variables are not automatically synced back.
        // Every state write in .ink (~ closed_off = true) must be read here.
        // This is a manual contract — a source of bugs if writers add new state vars
        // without updating this method. Production mitigation: codegen or a reflection loop.
        int newDepth = (int)_story.variablesState["depth_tier"];
        bool closedOff = (bool)_story.variablesState["closed_off"];

        _stateManager.SetDepthTier(_characterId, newDepth);
        if (closedOff) _stateManager.SetClosedOff(_characterId, true);
    }
}

// ── Minimal stubs (stand-ins for production systems) ─────────────────────────

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
    public string PronounSubject => "they"; // "he" | "she" | "they"
}
