using System;
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

            var currentDoc = dte.ActiveDocument;
            if (currentDoc == null)
            {
                throw new InvalidOperationException("No document currently open.");
            }

            // 1. get current file
            var fullName = currentDoc.FullName;

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
                Arguments = fullName,
            };

            // we're not waiting for p to exit in this thread because the client may hang around forever.
            Process.Start(psi);
        }

        /// <summary>
        /// General method trying to prevent the presence of UAC and UAC-based file-ownership from messing up for Emacs
        /// when it saves data back to disk from an unpriviliged proccess.
        /// </summary>
        /// <param name="fullName"></param>
        private void TryMakeWritable(string fullName)
        {
            FileInfo fi = new FileInfo(fullName);
            var everyoneUser = new SecurityIdentifier(WellKnownSidType.WorldSid, null);

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
                var rule = new FileSystemAccessRule(everyoneUser, FileSystemRights.FullControl, InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit, PropagationFlags.NoPropagateInherit, AccessControlType.Allow);
                dSecurity.AddAccessRule(rule);
                di.SetAccessControl(dSecurity);
            }
            catch { }
        }
    }
}
