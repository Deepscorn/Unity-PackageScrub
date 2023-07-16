# Unity-PackageScrub
Plugin to uninstall legacy unity plugins that are pasted as files and don't have "remove" button in UPM.

Plugin is still in early alpha.

What plugin does: it simply dumps filepath changes on package import. Those changes are called "package history". It can be used later to review installed files and remove them by hand or automatically.

It can remove installed plugins through menu. 

What plugin does not (still): check files that are reused between plugins. So use it with care (discard those shared files if needed from vcs) or remove by hand using the history files in PackageHistory directory in the root of the unity project.

## INSTALLATION

There are ways to install this plugin:

- clone/[download](https://github.com/Deepscorn/Unity-PackageScrub/archive/refs/heads/main.zip) this repository and move the *Plugins* folder to your Unity project's *Assets* folder\
- *(via Package Manager)* add the following line to *Packages/manifest.json*:
  - `"com.deepscorn.packagescrub": "https://github.com/Deepscorn/Unity-PackageScrub.git",`
