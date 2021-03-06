﻿using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.ComponentModel.Design;
using EnvDTE80;
using Microsoft.Win32;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using System.Security.AccessControl;
using System.IO;
using System.Security.Principal;
using EnvDTE;

namespace kjonigsennet.vsmode
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    ///
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the 
    /// IVsPackage interface and uses the registration attributes defined in the framework to 
    /// register itself and its components with the shell.
    /// </summary>
    // This attribute tells the PkgDef creation utility (CreatePkgDef.exe) that this class is
    // a package.
    [PackageRegistration(UseManagedResourcesOnly = true)]
    // This attribute is used to register the information needed to show this package
    // in the Help/About dialog of Visual Studio.
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    // This attribute is needed to let the shell know that this package exposes some menus.
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(GuidList.guidvsmodePkgString)]
    public sealed class vsmodePackage : Package
    {
        /// <summary>
        /// Default constructor of the package.
        /// Inside this method you can place any initialization code that does not require 
        /// any Visual Studio service because at this point the package object is created but 
        /// not sited yet inside Visual Studio environment. The place to do all the other 
        /// initialization is the Initialize method.
        /// </summary>
        public vsmodePackage()
        {
            Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering constructor for: {0}", this.ToString()));
        }



        /////////////////////////////////////////////////////////////////////////////
        // Overridden Package Implementation
        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override void Initialize()
        {
            Debug.WriteLine (string.Format(CultureInfo.CurrentCulture, "Entering Initialize() of: {0}", this.ToString()));
            base.Initialize();

            // Add our command handlers for menu (commands must exist in the .vsct file)
            OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if ( null != mcs )
            {
                // Create the command for the menu item.
                CommandID menuCommandID = new CommandID(GuidList.guidvsmodeCmdSet, (int)PkgCmdIDList.vsmodeOpenInEmacs);
                MenuCommand menuItem = new MenuCommand(MenuItemCallback, menuCommandID );
                mcs.AddCommand( menuItem );
            }
        }
        #endregion

        /// <summary>
        /// This function is the callback used to execute a command when the a menu item is clicked.
        /// See the Initialize method to see how the menu item is associated to this function using
        /// the OleMenuCommandService service and the MenuCommand class.
        /// </summary>
        private void MenuItemCallback(object sender, EventArgs e)
        {
            try
            {
                OpenInEmacs();
            }
            catch (Exception ex)
            {
                string msg = ex.ToString();
                DialogMessage(msg);
            }
        }

        private void DialogMessage(string msg)
        {
            IVsUIShell uiShell = (IVsUIShell) GetService(typeof (SVsUIShell));
            Guid clsid = Guid.Empty;
            int result;
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(uiShell.ShowMessageBox(
                0,
                ref clsid,
                "vsmode",
                msg,
                string.Empty,
                0,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST,
                OLEMSGICON.OLEMSGICON_INFO,
                0, // false
                out result));
        }

        private void OpenInEmacs()
        {
            var dte = GetService(typeof(Microsoft.VisualStudio.Shell.Interop.SDTE)) as DTE2;
            if (dte == null)
            {
                throw new Exception("Main application-object not available.");
            }

            string fullName = null;
            string position = null;

            var currentDoc = dte.ActiveDocument;
            if (currentDoc != null)
            {
                fullName = currentDoc.FullName;
                var selection = currentDoc.Selection as TextSelection;
                if (selection != null)
                {
                    position = string.Format("+{0}:{1} ", selection.CurrentLine, selection.CurrentColumn);
                }
            }
            else
            {
                var selectedItems = (UIHierarchyItem[])dte.ToolWindows.SolutionExplorer.SelectedItems;
                fullName = GetFullPathFromSelectedItems(selectedItems);
            }

            if (string.IsNullOrWhiteSpace(fullName))
            {
                throw new InvalidOperationException("No document currently open or selected.");
            }

            // 1. get current file

            // 2. get source-control status
            var sourceControl = dte.SourceControl;
            //var sc2 = sourceControl as SourceControl2;
            var shouldCheckOut =
                sourceControl.IsItemUnderSCC(fullName)
                && !sourceControl.IsItemCheckedOut(fullName);

            // 2.5. check out
            if (shouldCheckOut)
            {
                sourceControl.CheckOutItem(fullName);
            }

            // 2.6 reset file-permissions incase we're UACed.
            TryMakeWritable(fullName);

            // 3. send off to emacsclientw. assume in path.
            var psi = new ProcessStartInfo
            {
                // Ref a "proper" windows emacs-setup as found here:
                // https://www.gnu.org/software/emacs/manual/html_node/efaq-w32/Associate-files-with-Emacs.html#Associate-files-with-Emacs
                FileName = "emacsclientw",
                // Filename in arguments should be quoted if we want to handled spaces.
                Arguments = position + @"""" + fullName + @"""",
            };

            // we're not waiting for p to exit in this thread because the client may hang around forever.
            System.Diagnostics.Process.Start(psi);

            FocusEmacsWindow();
        }

        private string GetFullPathFromSelectedItems(UIHierarchyItem[] selectedItems)
        {
            if (selectedItems == null || selectedItems.Length == 0)
            {
                return null;
            }

            foreach (var selectedItem in selectedItems)
            {
                var solution = selectedItem.Object as Solution;
                if (solution != null)
                {
                    return solution.FullName;
                }

                var project = selectedItem.Object as Project;
                if (project != null)
                {
                    return project.FullName;
                }

                var projectItem = selectedItem.Object as ProjectItem;
                if (projectItem != null)
                {
                    return projectItem.Properties.Item("FullPath").Value.ToString();
                }
            }

            return null;
        }

        /// <summary>
        /// General method trying to prevent the presence of UAC and UAC-based file-ownership from messing up for Emacs
        /// when it saves data back to disk from an unpriviliged proccess.
        /// </summary>
        /// <param name="fullName"></param>
        private void TryMakeWritable(string fullName)
        {
            FileInfo fi = new FileInfo(fullName);

            // .Translate() needed to resolve {S-1-1-0} to "Everyone", which is required for later comparison!
            IdentityReference everyoneUser = new SecurityIdentifier(WellKnownSidType.WorldSid, null).Translate(typeof(NTAccount));

            // ensure emacs has rights to write to the actual file.
            try
            {
                FileSecurity fSecurity = fi.GetAccessControl();
                var rule = new FileSystemAccessRule(everyoneUser, FileSystemRights.FullControl, AccessControlType.Allow);
                fSecurity.SetAccessRule(rule);

                fi.SetAccessControl(fSecurity);
            }
            catch { }

            // we (not admin) need to own the file for Emacs to be able to handle backups etc without ACL failures.
            try
            {
                FileSecurity fSecurity = fi.GetAccessControl();
                fSecurity.SetOwner(new NTAccount(Environment.UserDomainName, Environment.UserName));
                fi.SetAccessControl(fSecurity);
            }
            catch { }

            // if we want to avoid having write-failures because of ACLs, we may need to ensure directory is writable too.
            DirectoryInfo di = fi.Directory;
            try
            {
                DirectorySecurity dSecurity = di.GetAccessControl();
                var rules = dSecurity.GetAccessRules(true, true, typeof(System.Security.Principal.NTAccount));
                foreach (FileSystemAccessRule rule in rules)
                {
                    if (rule.FileSystemRights == FileSystemRights.FullControl && rule.IdentityReference == everyoneUser)
                    {
                        // equivalent rule already present.
                        // do NOT set (setting folder permissions needs to propagate === very, very slow)
                        return;
                    }
                }

                // seems we need to set a new rule. let's do it!
                var everyoneRule = new FileSystemAccessRule(everyoneUser, FileSystemRights.FullControl, InheritanceFlags.None, PropagationFlags.NoPropagateInherit, AccessControlType.Allow);
                dSecurity.AddAccessRule(everyoneRule);
                di.SetAccessControl(dSecurity);
            }
            catch { }
        }

        /// <summary>
        /// By default emacsclientw will only make emacs open the file, it will not cause Emacs to regain focus.
        /// Hack this into happening.
        /// </summary>
        private void FocusEmacsWindow()
        {
            var emacsen = System.Diagnostics.Process.GetProcessesByName("emacs");

            if (emacsen.Length == 0)
            {
                // sleep a little to give emacs time to show up.
                System.Threading.Thread.Sleep(1000);
            }

            emacsen = System.Diagnostics.Process.GetProcessesByName("emacs");
            if (emacsen.Length == 0)
            {
                // nothing we can do.
                return;
            }

            // if there is more than one... to bad!
            var emacs = emacsen[0];
            if (emacs.MainWindowHandle == IntPtr.Zero)
            {
                // not sure how this would happen, but passing 0 along wont work.
                // better safe than sorry.
                return;
            }
            SetForegroundWindow(emacs.MainWindowHandle);
        }

        [DllImport("USER32.DLL")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}
