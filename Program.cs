// <copyright file="Program.cs" company="Educational project">
// Copyright (c) Educational project. All rights reserved.
// </copyright>

namespace Dihotomia
{
    using System;
    using System.Windows.Forms;

    /// <summary>
    /// Contains the application entry point.
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// Starts the WinForms application.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
