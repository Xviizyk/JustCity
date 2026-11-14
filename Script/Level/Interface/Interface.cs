using Godot;

namespace JustCity.Script.Level.Interface;

public partial class Interface : CanvasLayer
{
    [ExportGroup("Nodes")]
    [Export] public Label BalanceLabel;

    [ExportGroup("Settings")]
    [Export] public float BalanceRefreshTimeout = 0.25f;

    public double _balanceRefreshTimer = 0.0;

    public override void _Process(double delta)
    {
        _balanceRefreshTimer += delta;

        if (_balanceRefreshTimer >= BalanceRefreshTimeout)
        {
            _balanceRefreshTimer = 0.0;
            BalanceLabel.Text = Core.Core.Instance.CurrentSaveData.Balance.ToString();
        }
    }
}