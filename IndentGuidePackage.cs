﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.TextManager.Interop;

namespace IndentGuide
{
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", "8.0", IconResourceID = 400)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.NoSolution_string)]
    [ProvideMenuResource(1000, 1)]
    [ProvideOptionPage(typeof(DisplayOptions), "IndentGuide", "Display", 110, 120, true)]
    [ProvideProfile(typeof(DisplayOptions), "IndentGuide", "Display", 110, 120, true)]
    [ProvideService(typeof(SIndentGuide))]
    [ResourceDescription("IndentGuidePackage")]
    [Guid("959BEB25-6C38-440A-A37F-5D6717E9A41B")]
    public sealed class IndentGuidePackage : Package
    {
        public IndentGuidePackage()
        {
            var container = (IServiceContainer)this;
            var callback = new ServiceCreatorCallback(CreateService);
            container.AddService(typeof(SIndentGuide), callback, true);
        }

        private object CreateService(IServiceContainer container, Type serviceType)
        {
            if (typeof(SIndentGuide) == serviceType)
            {
                return new IndentGuideService();
            }
            else
                return null;
        }

        private static readonly Guid guidIndentGuideCmdSet = Guid.Parse("1AE9DCDB-7723-4651-ABDC-3D4BBAA0731F");
        private const int cmdidViewIndentGuides = 0x0102;

        private EnvDTE.WindowEvents WindowEvents;
        private IIndentGuide Service;

        protected override void Initialize()
        {
            Trace.WriteLine("Entering Initialise() of IndentGuide");
            base.Initialize();

            // Adding the command is deferred to reduce VS start-up time.
            _menuCommand = null;

            // Prepare event
            var dte = GetService(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
            if (dte != null)
            {
                WindowEvents = dte.Events.WindowEvents;
                WindowEvents.WindowActivated += 
                    new EnvDTE._dispWindowEvents_WindowActivatedEventHandler(WindowEvents_WindowActivated);
            }

            // Assume the service exists, otherwise, crash the extension.
            var service = (SIndentGuide)GetService(typeof(SIndentGuide));
            
            Service = (IIndentGuide)service;
            Service.VisibleChanged += new EventHandler(Service_VisibleChanged);
            
            service.Initialize(dte);
        }

        private MenuCommand _menuCommand;
        /// <summary>
        /// Lazily initialises and returns the menu item for showing
        /// and hiding indent guides.
        /// </summary>
        /// <returns>An instance of <see cref="MenuCommand"/> or
        /// <b>null</b>.</returns>
        MenuCommand GetCommand()
        {
            if (_menuCommand != null) return _menuCommand;

            // Add our command handlers for menu (commands must exist in the .vsct file)
            var mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            
            if (mcs != null)
            {
                // Create the command for the tool window
                CommandID viewIndentCommandID = new CommandID(guidIndentGuideCmdSet, cmdidViewIndentGuides);
                var menuCmd = new MenuCommand(ToggleVisibility, viewIndentCommandID);
                menuCmd.Checked = Service.Visible;

                if (System.Threading.Interlocked.CompareExchange(ref _menuCommand, menuCmd, null) == null)
                {
                    mcs.AddCommand(_menuCommand);
                    return _menuCommand;
                }
            }

            return null;
        }

        void WindowEvents_WindowActivated(EnvDTE.Window GotFocus, EnvDTE.Window LostFocus)
        {
            var menuCmd = GetCommand();
            if (menuCmd == null) return;
            
            menuCmd.Visible = menuCmd.Enabled = (GotFocus != null && GotFocus.Kind == "Document");
        }

        private void ToggleVisibility(object sender, EventArgs e)
        {
            Service.Visible = !Service.Visible;
        }
        
        void Service_VisibleChanged(object sender, EventArgs e)
        {
            var service = sender as IIndentGuide;
            if (service == null) return;
            var menuCmd = GetCommand();
            if (menuCmd == null) return;
            
            menuCmd.Checked = service.Visible;
        }

    }
}