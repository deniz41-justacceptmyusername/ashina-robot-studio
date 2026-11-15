# Contributing

Useful contributions include clearer robot-building workflows, reproducible physics fixes, NT data-format support, accessibility, and driver-training examples.

1. Open an issue describing the observed behavior and a small reproduction.
2. Keep changes focused. Preserve existing project files and user robot work.
3. Run `python scripts/verify-repo.py`.
4. For runtime changes, build with the recorded Unity version and test the affected behavior in the native player. Include the relevant log/result or a short recording.
5. Submit a pull request explaining the trigger, resulting behavior, and validation performed.

Do not commit Unity caches, compiled players, tokens, personal robot CAD, or team-specific robot code without the owner's explicit intent. Include attribution and license notices for new assets.

Changes to saved-project fields should keep existing version-2 projects readable or provide an explicit migration. Keep velocity, release attitude, and angular speed as separate concepts.

Contributions to original project code are submitted under the repository's MIT license. Third-party components retain their existing licenses.
