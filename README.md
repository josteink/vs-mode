
# vs-mode

vs-mode is a Visual Studio package meant to provide limited integrations against GNU Emacs.

## Dependencies

vs-mode is packaged as a regular VSIX package targetting Visual Studio 2013 & 2015.

This means "the usual" dependencies like Microsoft Extensibility Framework and friends are required.

## Features

Currently the package provides the following features:

* Open in Emacs
* Automatically check out files from source-control before opening. This is particularly useful for TFS source-control which Emacs does not support.
* Automatically fix broken file-permissions caused by UAC with Visual Studio opened as admin.
* Automatically focus Emacs-window after opening file.

## Building and using

Using this package involves a few steps:

* Open and build the solution.
* Install to Visual studio by opening the `vsmode.vsix`-file found in the target folder.
* Optionally: Bind "Open in Emacs" command to a key (like `M-x`) using Tools / Customize / Keyboard. The name of the command is `Tools.OpeninEmacs`.

