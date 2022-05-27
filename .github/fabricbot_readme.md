FabricBot readme
================

[FabricBot][0] is an automated tool that responds to changes in issues, PRs, or
runs on a schedule. It can modify issues and PRs by adding/removing labels,
adding comments, or performing other operations.

Those rules are all defined in the associated
[fabricbot.json](/.github/fabricbot.json) file in this folder.

While you can _try_ to edit this file manually, you can instead use the Fabric
Bot editor UI to modify the file in a more reliable manner:

1. Download the [fabricbot.json](/.github/fabricbot.json) file to your local disk
2. Go to https://portal.fabricbot.ms/bot/ (don't log in!)
3. Click **Choose File** and select the JSON file that you downloaded
4. And press Submit to load the file and launch the FabricBot rules editor
5. In this UI you can:
   - View all existing rules
   - Click **Add Task** in the upper-right corner to add a new task. This is
     where you can see the various capabilities of FabricBot
6. And then click around in the tool to see the various abilities and options

Don't worry, you can click all you want, because that UI won't save anything
to anywhere. You can export the rules and download the generated JSON file.
But don't worry about accidentally changing the FabricBot rules in this repo 😊

If you want to save your changes, click the **Export configuration** file, and
upload the new file to the repo's rules at `/.github/fabricbot.json`.

[0]: https://1esdocs.azurewebsites.net/datainsights/merlinbot/extensions/Fabricbot_Overview.html
