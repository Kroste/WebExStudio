using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using NLog;
using WebExStudio.Core.Localization;
using WebExStudio.UI.Controls;
using WebExStudio.UI.ViewModels;

namespace WebExStudio.UI.Views;

// Teil von FlowEditorView (Code-behind) — Aufteilung nach Verantwortlichkeit.
partial class FlowEditorView
{
    // ── Group context menu ─────────────────────────────────────────────────────

    private void ShowGroupMenu(GroupViewModel group)
    {
        if (Vm is null) return;
        var menu = new ContextMenu();

        var extract = new MenuItem { Header = Loc.T("Menu_SetupSubnode") };
        extract.Click += async (_, _) => await ExtractGroupAsync(group);

        var rename = new MenuItem { Header = Loc.T("Menu_Rename") };
        rename.Click += async (_, _) => await RenameGroupAsync(group);

        var ungroup = new MenuItem { Header = Loc.T("Menu_Ungroup") };
        ungroup.Click += (_, _) => Vm.Ungroup(group);

        menu.Items.Add(extract);
        menu.Items.Add(rename);
        menu.Items.Add(new Separator());
        menu.Items.Add(ungroup);
        menu.Open(this);
    }

    private async Task ExtractGroupAsync(GroupViewModel group)
    {
        if (Vm is null) return;
        var dlg = new SubnodeDialog(Loc.T("SubDlg_FromGroup"), "", group.Label);
        await ShowDialogOverOwner(dlg);
        if (!dlg.Confirmed) return;

        var sub = Vm.ExtractGroupToSubnode(group, dlg.SubnodeName, dlg.SubnodeLabel);
        if (sub is null)
            Log.Warn("Subnode konnte nicht erstellt werden (Name leer oder vergeben): {0}", dlg.SubnodeName);
        else
            Log.Info("Gruppe '{0}' → Subnode '{1}'", group.Label, dlg.SubnodeName);
    }

    private async Task RenameGroupAsync(GroupViewModel group)
    {
        if (Vm is null) return;
        var dlg = new SubnodeDialog(Loc.T("SubDlg_Rename"), group.Label, "");
        await ShowDialogOverOwner(dlg);
        if (!dlg.Confirmed) return;
        Vm.RenameGroup(group, dlg.SubnodeName);
        if (_groupControls.TryGetValue(group.Id, out var ctrl)) ctrl.RefreshLabel();
    }

    private async Task ShowDialogOverOwner(Window dlg)
    {
        if (TopLevel.GetTopLevel(this) is Window owner)
            await dlg.ShowDialog(owner);
        else
            dlg.Show();
    }

    private void UpdateTabButtonStyles() { /* active-tab highlight handled by binding */ }

    // ── Tab bar interactions ──────────────────────────────────────────────────

    private void OnTabClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Control { Tag: FlowTabViewModel tab })
            Vm?.SwitchTab(tab);
    }

    private void OnTabClose(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Control { Tag: FlowTabViewModel tab })
            Vm?.CloseTab(tab);
    }
}
