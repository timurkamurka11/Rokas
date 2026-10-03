# Unrelated EditMode failure source/resource review

Read-only review; no Unity launch and no protected project-file changes.

Baseline and current HEAD: `0eecaaf7ab62afbf5c949430f08cd1b175c9edc4`.
Archived XML: `D:\Rokas\reactiveturns-b2-staging\final-vfx-unity\edit-results.attempt-1.xml`; 938/943 passed, 5 failed.

## Exact comparisons

Normalization removes only UTF-8 BOM and CR. Spaces and LF are preserved.
Full raw and normalized SHA-256 hashes are in the companion JSON.

| File | HEAD exists | Current exists | Raw equal | BOM/CR normalized equal |
| --- | --- | --- | --- | --- |
| `Assets/Rokas/Tests/EditMode/HubDialoguePlaybackTests.cs` | True | True | False | True |
| `Assets/Rokas/Tests/EditorWorkshop/VnRuntimeIntroPackageTests.cs` | True | True | False | True |
| `Assets/Rokas/Tests/EditorWorkshop/VnSceneComposerMgTests.cs` | True | True | False | True |
| `Assets/Rokas/Tests/EditorWorkshop/VnSceneComposerPlaybackTests.FocusedAnimationPreview.cs` | True | True | False | True |
| `Assets/Rokas/Tests/EditorWorkshop/VnSceneComposerWindowTests.cs` | True | True | False | True |
| `Assets/Rokas/Scripts/Presentation/HubDialogueConfig.cs` | True | True | False | True |
| `Assets/Rokas/Scripts/Presentation/VnCharacterVisualStates.cs` | True | True | False | True |
| `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnPresentationWorkshopWindow.SceneComposer.cs` | True | True | False | True |
| `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnSceneComposerCharacterStateResolver.cs` | True | True | False | True |
| `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnSceneComposerAssetLibrary.cs` | True | True | False | True |
| `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnSceneComposerMediaEditing.cs` | True | True | False | True |
| `Assets/Rokas/Scripts/Editor/VnUiWorkshop/RokasVnRuntimeIntroExporter.cs` | True | True | False | True |
| `Assets/Rokas/Resources/HubDialogue/HubDialogueConfig.json` | True | True | False | True |
| `Assets/Rokas/Resources/HubDialogue/HubDialogueConfig.user.json` | False | True | False | n/a |
| `Assets/Rokas/Scripts/Editor/VnUiWorkshop/OnboardedAssets/ROKAS_VN_SCENE_COMPOSER_ASSET_CATALOG.json` | True | True | True | True |
| `Assets/Rokas/Scripts/Editor/VnUiWorkshop/OnboardedAssets/ROKAS_VN_SCENE_COMPOSER_ASSET_CATALOG.json.meta` | True | True | True | True |
| `Assets/Rokas/Scripts/Editor/VnUiWorkshop/OnboardedAssets/CharacterState/mina/9f18a6e3-9df2-4c27-9dbd-fe48df06b114.png` | True | True | True | None |
| `Assets/Rokas/Scripts/Editor/VnUiWorkshop/OnboardedAssets/CharacterState/mina/9f18a6e3-9df2-4c27-9dbd-fe48df06b114.png.meta` | True | True | True | True |
| `Assets/Rokas/Scripts/Editor/VnUiWorkshop/OnboardedAssets/CharacterState/mina/Mina serious.png` | True | True | True | None |
| `Assets/Rokas/Scripts/Editor/VnUiWorkshop/OnboardedAssets/CharacterState/mina/Mina serious.png.meta` | True | True | True | True |
| `Assets/Rokas/Scripts/Editor/VnUiWorkshop/OnboardedAssets/CharacterState/mina/Mina shy not happy.png` | True | True | True | None |
| `Assets/Rokas/Scripts/Editor/VnUiWorkshop/OnboardedAssets/CharacterState/mina/Mina shy not happy.png.meta` | True | True | True | True |
| `Assets/Rokas/Scripts/Editor/VnUiWorkshop/OnboardedAssets/CharacterState/mina/b5a221a0-4101-40f0-bc9c-cb2ad4563b9f.png` | True | True | True | None |
| `Assets/Rokas/Scripts/Editor/VnUiWorkshop/OnboardedAssets/CharacterState/mina/b5a221a0-4101-40f0-bc9c-cb2ad4563b9f.png.meta` | True | True | True | True |

## Classification

### Config_DefaultsUseEnglishKeikoAndEditableGuildIntro

Protected Home/Hub dialogue; local ignored user override. Default resource has speaker.y=128 at HEAD and current. Local user resource, absent from tracked HEAD, has y=232 and is selected first by unchanged LoadFresh.

### ProjectOwnedExternalPngPreservesNativeWidthThroughPreviewAndRuntimeExport

Protected VN export IO/AssetDatabase fixture. Test/exporter/media/library are BOM/CR normalized identical to HEAD; generated source imported successfully and CopyAsset returned false. Log imports numbered fixture siblings 7..12. EnsureAssetFolder ignores CreateFolder's returned GUID and advances the requested path; this is unchanged baseline code. Specific runtime cause remains unproven.

### MG_InspectorKeepsRecognizableSectionsAndDedicatedSelector

Protected VN stale static assertion. Identical HEAD/current source has 13 sections (including Movement/Effects), while identical test requires 11.

### FocusedAnimationPreviewButtonsAreWiredToFocusedPreviewAction

Protected VN stale source-string assertion. Exact tested method is identical after BOM/CR normalization at HEAD/current. It calls ComposerPreviewSelectedSceneBoundaryTransition, and lacks the test's BackgroundTransition literal at both states.

### AuthoredCharacterPickerOnlyReturnsCatalogStatesAndAddCharacterUsesSelectedState

Protected VN stale legacy-catalog assertion. Unchanged resolver combines production and onboarded states. Tracked HEAD/current catalog and assets already include four valid onboarded Mina ids not present in unchanged legacy VnCharacterVisualCatalog. Test requires every returned id to resolve in the legacy catalog.

## Limitations

- No Unity baseline run was performed. Source/resource equality alone does not prove historical runtime test failure.
- AssetDatabase.CopyAsset returned false in the tested run; the XML does not expose the underlying IO/AssetDatabase cause.
- Generated VN runtime PNG fixture files were cleaned up by the test, so their post-failure contents are not available.
- The actual first rejected character state id is not included in the failure message; static catalog mismatches are identified separately.

The full EditMode run is red. This review does not relabel it as a green suite or prove a historical baseline Unity run.
