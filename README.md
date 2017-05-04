
# vs-mode

vs-mode is a Visual Studio package meant to provide limited integrations against GNU Emacs.

## Features

Currently the package provides the following features:

* Open in Emacs
* Automatically check out files from source-control before opening. This is particularly useful for TFS source-control which Emacs does not support.
* Automatically fix broken file-permissions caused by UAC with Visual Studio opened as admin.
* Automatically focus Emacs-window after opening file.

## Installation

* Go to [releases](https://github.com/josteink/vs-mode/releases) and download the latest VSIX-file.
* Once downloaded, simply run the file and check the versions of Visual Studio you want to install it for.
* Optionally: Bind "Open in Emacs" command to a key (like `M-x`) using Tools / Customize / Keyboard. The name of the command is `Tools.OpeninEmacs`.

## Dependencies

vs-mode is packaged as a regular VSIX package targetting Visual Studio 2013 & 2015.

This means "the usual" dependencies like Microsoft Extensibility Framework and friends are required.

## Building

Bulding this package involves a few steps:

* Open and build the solution using Visual Studio 2015.
* Install to Visual studio by opening the `vsmode.vsix`-file found in the target folder.


