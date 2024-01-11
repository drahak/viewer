﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace Viewer.Core.UI
{
    public interface IWindowView : IDockContent, IDisposable
    {
        /// <summary>
        /// Event called after the view closed.
        /// Note: it is up to the view to define what does it mean for it to be closed
        ///       (i.e. it might just hide itself or it might actually close its form)
        /// </summary>
        event EventHandler CloseView;

        /// <summary>
        /// Event occurs when this view gets focus.
        /// </summary>
        event EventHandler ViewActivated;

        /// <summary>
        /// Event occurs when this view loses focus.
        /// </summary>
        event EventHandler ViewDeactivated;

        /// <summary>
        /// Dock panel of this window or null if the window is not docked.
        /// </summary>
        DockPanel DockPanel { get; }

        /// <summary>
        /// Name of the window
        /// </summary>
        string Text { get; set; }

        /// <summary>
        /// true iff the caller has to use an Invoke method in order to modify the view.
        /// </summary>
        bool InvokeRequired { get; }

        /// <summary>
        /// Get currently pressed modified keys (i.e., state of Control, Shift and Alt keys)
        /// </summary>
        Keys ModifierKeyState { get; }

        /// <summary>
        /// Check whether this window is activated (i.e., it has focus)
        /// </summary>
        bool IsActivated { get; }

        /// <summary>
        /// Show the window in <paramref name="dockPanel"/>
        /// </summary>
        /// <param name="dockPanel">A new dock panel for this window</param>
        void Show(DockPanel dockPanel);

        /// <summary>
        /// Show the window in <paramref name="dockPanel"/> with <paramref name="dockState"/>
        /// </summary>
        /// <param name="dockPanel">A new dock panel for this window</param>
        /// <param name="dockState">Dock state</param>
        void Show(DockPanel dockPanel, DockState dockState);

        /// <summary>
        /// Hide this window.
        /// </summary>
        void Hide();

        /// <summary>
        /// Close this window.
        /// </summary>
        void Close();

        /// <summary>
        /// Make sure the window is visible to the user.
        /// </summary>
        void EnsureVisible();

        /// <summary>
        /// Execute <paramref name="method"/> on the thread of this view.
        /// </summary>
        /// <param name="method">Method to execute</param>
        /// <returns></returns>
        IAsyncResult BeginInvoke(Delegate method);

        /// <summary>
        /// Execute <paramref name="method"/> od the thread of this view.
        /// </summary>
        /// <param name="method">Method to execute</param>
        /// <returns></returns>
        object Invoke(Delegate method);

        /// <summary>
        /// Indicate to the user that the component is loading some data
        /// </summary>
        void BeginLoading();

        /// <summary>
        /// Previous loading operation ended
        /// </summary>
        void EndLoading();
    }
}
