# Manual UI checklist

Run before tagging a release. Use a generated or real game folder.

## Start
- [ ] Start with no game in the exe folder: startup errors show at the top of the page, "Choose game folder..." works and restarts.
- [ ] Start in a valid folder: status bar shows the edition and folder, Randomize is enabled.
- [ ] Close and reopen: window size, position, maximized state and theme are restored.
- [ ] Move the window to a second monitor, unplug it, reopen: the window appears on a visible screen.
- [ ] Corrupt `enemyRandomizerData/lastConfig.json`: the app still starts with default settings.

## Keyboard only
- [ ] Tab reaches the seed box, the module list, the on/off switch, every option, enemy config, Backups, Randomize. Focus ring is always visible.
- [ ] Every option shows its description (or why it is disabled) under its title; radio buttons show theirs as tooltip.
- [ ] Alt+M opens the menu; Ctrl+R randomizes; F1 opens About; Esc cancels Save As in the editor.
- [ ] Nothing behind the progress overlay can be reached with Tab.

## Screen reader (Narrator)
- [ ] Options announce name and help text; the progress message and result banner are announced.

## Operations
- [ ] Randomize: overlay with progress, success banner, "Open folder" opens `enemyRandomizerData/refs`.
- [ ] Randomize then Cancel: warning banner, then Randomize again and Revert both work.
- [ ] Revert: Yes, No and Cancel at the question behave as labelled. No Cancel button during the revert.
- [ ] Copy placement with a good and a malformed file.
- [ ] Force an error (read-only game folder): error banner, "Open log folder" and "Copy details" work, log has the stack.

## Modules and options
- [ ] The sidebar lists Enemy Randomizer (On) and Item Randomizer (Coming soon, cannot be switched on).
- [ ] Switch the enemy module off: all options and the enemy config grey out, Randomize is disabled, Revert still works. Restart: it is still off.
- [ ] Random vs Shuffle: the options that never apply disappear and empty categories vanish; options that only depend on another option are greyed with a reason, no "[No effect]" text anywhere.
- [ ] "Replace NPC-s" offers only Do not replace / With normal enemies.

## Dialogs
- [ ] Backups: list matches the `.bak` files in the game folder, Verify re-checks, "Select randomized" + "Restore selected" restores them after confirming; a tiny `.bak` shows as broken and cannot be ticked.
- [ ] Text config: Generate, Copy, Apply with valid and invalid text (inline error, cleared on edit).
- [ ] Enemy config editor: invalid name shows an error and disables Save; existing name shows the replace hint.

## Look
- [ ] Light, dark and Windows high-contrast mode; check primary button, on/off switch, radio groups, drop-downs, sliders, menu popups, DataGrid.
- [ ] 100%, 150% and 200% display scaling; resize to 760x560 (nothing clipped, labels wrap, the sidebar stays usable).
