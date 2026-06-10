// Global usings for Terminal.Gui v2.4.x namespace layout.
// Terminal.Gui 2.1+ split the former flat `Terminal.Gui` namespace into sub-namespaces
// (App / ViewBase / Views / Drawing / Input / Text / Configuration). These global usings
// keep the application source free of per-file using churn after the 2.0 -> 2.4 upgrade.
global using Terminal.Gui.App;
global using Terminal.Gui.ViewBase;
global using Terminal.Gui.Views;
global using Terminal.Gui.Drawing;
global using Terminal.Gui.Input;
global using Terminal.Gui.Drivers;
global using Terminal.Gui.Text;
global using Terminal.Gui.Configuration;

// `Attribute` is ambiguous between System.Attribute and Terminal.Gui.Drawing.Attribute once
// the Drawing namespace is imported globally. Alias the bare name to the Terminal.Gui type,
// which is the only one the UI code uses.
global using Attribute = Terminal.Gui.Drawing.Attribute;
