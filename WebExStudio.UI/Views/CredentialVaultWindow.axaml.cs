using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using WebExStudio.Core.Credentials;

namespace WebExStudio.UI.Views;

public partial class CredentialVaultWindow : Window
{
    private readonly CredentialVault _vault;
    private readonly Func<Task>? _persist; // schreibt den Flow (mit eingebettetem Tresor) auf die Platte

    public CredentialVaultWindow() : this(new CredentialVault()) { }

    public CredentialVaultWindow(CredentialVault vault, Func<Task>? persist = null)
    {
        InitializeComponent();
        _vault = vault;
        _persist = persist;
        if (_vault.IsUnlocked)
            ShowManage();
        else
        {
            UnlockHint.Text = _vault.HasData
                ? "Master-Passwort eingeben, um den Tresor dieses Flows zu entsperren."
                : "Noch kein Tresor in diesem Flow — Master-Passwort festlegen, um einen anzulegen.";
            UnlockButton.Content = _vault.HasData ? "Entsperren" : "Anlegen";
            Opened += (_, _) => UnlockPwBox.Focus();
        }
    }

    /// <summary>Persistiert den Flow (mit dem in <c>_vault.Save()</c> eingebetteten Tresor) auf die Platte.</summary>
    private async Task PersistAsync()
    {
        if (_persist is not null) await _persist();
    }

    private void OnUnlock(object? sender, RoutedEventArgs e)
    {
        var pw = UnlockPwBox.Text ?? string.Empty;
        if (pw.Length == 0) { ShowUnlockError("Bitte ein Master-Passwort eingeben."); return; }
        try
        {
            _vault.Unlock(pw); // ohne Daten startet der Tresor leer; persistiert wird erst beim Anlegen eines Eintrags
            ShowManage();
        }
        catch
        {
            ShowUnlockError("Falsches Master-Passwort.");
        }
    }

    private void ShowUnlockError(string msg)
    {
        UnlockError.Text = msg;
        UnlockError.IsVisible = true;
    }

    private void OnUnlockKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { OnUnlock(sender, e); e.Handled = true; }
    }

    private void ShowManage()
    {
        UnlockPanel.IsVisible = false;
        ManagePanel.IsVisible = true;
        RefreshNames(null);
    }

    private void RefreshNames(string? select)
    {
        NamesList.ItemsSource = _vault.Names;
        if (select is not null) NamesList.SelectedItem = select;
        else if (NamesList.ItemCount > 0) NamesList.SelectedIndex = 0;
        else LoadEntry(null);
    }

    private void OnSelectEntry(object? sender, SelectionChangedEventArgs e) =>
        LoadEntry(NamesList.SelectedItem as string);

    private void LoadEntry(string? name)
    {
        if (name is null)
        {
            EntryTitle.Text = "Kein Eintrag gewählt";
            UserBox.Text = PassBox.Text = ApiBox.Text = string.Empty;
            return;
        }
        EntryTitle.Text = $"Eintrag: {name}";
        var entry = _vault.Entry(name);
        UserBox.Text = entry?.GetValueOrDefault("user") ?? string.Empty;
        PassBox.Text = entry?.GetValueOrDefault("password") ?? string.Empty;
        ApiBox.Text = entry?.GetValueOrDefault("api") ?? string.Empty;
    }

    private async void OnAddEntry(object? sender, RoutedEventArgs e)
    {
        var name = (NewNameBox.Text ?? string.Empty).Trim();
        if (name.Length == 0) { ManageStatus.Text = "Bitte einen Namen eingeben."; return; }
        if (_vault.Entry(name) is null) _vault.SetEntry(name, new Dictionary<string, string>());
        _vault.Save();
        await PersistAsync();
        NewNameBox.Text = string.Empty;
        RefreshNames(name);
        ManageStatus.Text = $"Eintrag „{name}“ angelegt.";
    }

    private async void OnDeleteEntry(object? sender, RoutedEventArgs e)
    {
        if (NamesList.SelectedItem is not string name) return;
        _vault.RemoveEntry(name);
        _vault.Save();
        await PersistAsync();
        RefreshNames(null);
        ManageStatus.Text = $"Eintrag „{name}“ gelöscht.";
    }

    private async void OnSaveEntry(object? sender, RoutedEventArgs e)
    {
        if (NamesList.SelectedItem is not string name) { ManageStatus.Text = "Kein Eintrag gewählt."; return; }
        // Bestehende Felder beibehalten (z. B. eigene), die Standardfelder aktualisieren.
        var fields = new Dictionary<string, string>(_vault.Entry(name) ?? new Dictionary<string, string>());
        SetOrRemove(fields, "user", UserBox.Text);
        SetOrRemove(fields, "password", PassBox.Text);
        SetOrRemove(fields, "api", ApiBox.Text);
        _vault.SetEntry(name, fields);
        _vault.Save();
        await PersistAsync();
        ManageStatus.Text = $"„{name}“ gespeichert.";
    }

    private static void SetOrRemove(Dictionary<string, string> d, string key, string? value)
    {
        if (string.IsNullOrEmpty(value)) d.Remove(key);
        else d[key] = value;
    }

    private async void OnChangePassword(object? sender, RoutedEventArgs e)
    {
        var dlg = new PasswordDialog("Master-Passwort ändern", "Neues Master-Passwort:", confirm: true);
        await dlg.ShowDialog(this);
        if (!dlg.Confirmed) return;
        _vault.ChangePassword(dlg.Password);
        await PersistAsync();
        ManageStatus.Text = "Master-Passwort geändert.";
    }

    private void OnLock(object? sender, RoutedEventArgs e)
    {
        _vault.Lock();
        Close();
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }
}
