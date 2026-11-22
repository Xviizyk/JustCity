using System;
using System.Collections.Generic;
using Godot;
using JustCity.Script.Core.Save;
using JustCity.Script.Level.Building.Road.Geometry;
using JustCity.Script.Level.Building.Road.Graph;
using JustCity.Script.Level.Building.Road.View;
using JustCity.Script.Level.Player;

namespace JustCity.Script.Level.Building.Road.Tool;

public enum BuildMode { Build, Clear, Parallel, Turn }

public enum BuildType { Straight, Grid, GridSmooth, Smooth, Circular, Smart }

public partial class RoadBuilder : Node2D
{
	[ExportGroup("Nodes")]
	[Export] public RoadRenderer Renderer;
	[Export] public RoadOverlay Overlay;
	[Export] public InputHandler InputHandlerer;

	[ExportGroup("State")]
	[Export] public RoadGraph Network;
	[Export] public bool Active = true;

	[ExportGroup("Road")]
	[Export] public int LanesPerDirection = 1;
	[Export] public float LaneWidth = 12f;
	[Export] public int Level = 0;
	[Export] public BuildType Type = BuildType.Smart;

	[ExportGroup("Snapping")]
	[Export] public float GridSize = 0f;
	[Export] public float SnapRadius = 20f;
	[Export] public float SnapPx = 10f;
	[Export] public float DeadZonePx = 10f;
	[Export] public float LengthStep = 8f;
	[Export] public float AngleStepDeg = 15f;
	[Export] public float RhythmStep = 96f;
	[Export] public float AutoDirConeDeg = 35f;
	[Export] public int LaneSnapMinLanes = 2;

	[ExportGroup("Rules")]
	[Export] public float MinRadius = 30f;
	[Export] public float MinLength = 40f;
	[Export] public float MinJunctionAngleDeg = 90f;
	[Export] public float JunctionAngleTolerance = 3f;
	[Export] public int MaxJunctionArms = 4;
	[Export] public float CostPerPixel = 0.5f;
	[Export] public float ClearCostPerPixel = 0.25f;
	[Export] public float BridgeCostMultiplier = 2f;
	[Export] public int MaxLanes = 4;

	[ExportGroup("Parallel")]
	[Export] public float ParallelStep = 8f;
	[Export] public float ParallelMargin = 4f;

	[ExportGroup("Feel")]
	[Export] public float PreviewSmoothing = 28f;
	[Export] public float ClickBuffer = 0.08f;

	[ExportGroup("Debug")]
	[Export] public bool ShowDebug = false;

	public BuildTool Build;
	public ClearTool Clear;
	public ParallelTool Parallel;
	public TurnTool Turn;
	public RoadSnapper Snapper;
	public RoadHistory History = new();
	public SaveData Data;

	public BuildMode Mode = BuildMode.Build;
	public RoadSnap Hover = new(Vector2.Zero);
	public string SnapKeyNow = "";
	public Vector2 RingPosition;
	public float RingRadius = 9f;
	public float BadAmount;
	public float Shake;
	public float Zoom = 1f;
	public bool Shift;
	public bool Alt;

	private string snapKey = "";
	private int stateKey;

	public override void _Ready()
	{
		if (Renderer == null || Overlay == null)
		{
			GD.PushError("RoadBuilder: поля Renderer и Overlay нужно назначить в сцене.");
			Active = false;
			return;
		}
		Build = new BuildTool(this);
		Clear = new ClearTool(this);
		Parallel = new ParallelTool(this);
		Turn = new TurnTool(this);
		Snapper = new RoadSnapper(this);

		Data = Core.Core.Instance.CurrentSaveData;
		Data.RoadNetwork ??= new RoadGraph();
		Network = Data.RoadNetwork;
		Renderer.Network = Network;
		Renderer.ShowDebug = ShowDebug;
	}

	public override void _Process(double delta)
	{
		if (!Active || Network == null) return;
		float dt = (float)delta;
		Vector2 mouse = GetLocalMousePosition();
		Zoom = Mathf.Max(0.01f, GetGlobalTransformWithCanvas().Scale.X);
		Shift = InputHandlerer.IsShifting;
		Alt = InputHandlerer.IsAlting;

		int key = HashCode.Combine(mouse.X, mouse.Y, Shift, Alt, Level, LanesPerDirection, Network.Version);
		key = HashCode.Combine(key, Mode, Zoom, GridSize, (int)Data.Balance, Build.Placing, Build.Chain, Build.Start.Position);
		key = HashCode.Combine(key, Clear.Clearing, Parallel.Reference, Turn.Node, Turn.FromArm);
		bool moved = key != stateKey;
		stateKey = key;

		if (moved)
		{
			SnapKeyNow = "";
			if (Mode == BuildMode.Clear) Hover = Clear.Snap(mouse);
			else
			{
				if (Build.Placing) Build.UpdateDir(mouse);
				Hover = Snapper.Find(mouse);
			}
			if (SnapKeyNow != snapKey && SnapKeyNow != "" && Hover.Node != null) RingRadius = 15f;
			snapKey = SnapKeyNow;
		}
		Tool().Update(mouse, dt, moved);

		Shake = Mathf.Max(0f, Shake - dt);
		RingPosition = RingPosition.Lerp(Hover.Position, Hover.Free ? 1f : 1f - Mathf.Exp(-30f * dt));
		RingRadius = Mathf.Lerp(RingRadius, 9f, 1f - Mathf.Exp(-22f * dt));

		bool bad = false;
		if (Mode == BuildMode.Build) bad = Build.Placing && Build.Curve != null && !Build.Valid;
		if (Mode == BuildMode.Parallel) bad = Parallel.Reference != null && Parallel.Curve != null && !Parallel.Valid;
		BadAmount = Mathf.MoveToward(BadAmount, bad ? 1f : 0f, dt * 8f);

		Overlay.QueueRedraw();
	}

	public override void _ExitTree()
	{
		if (Network != null) Data.RoadNetwork = Network;
	}

	public void NextBuildType()
	{
		Type = (BuildType)(((int)Type + 1) % 6);
	}

	private RoadTool Tool()
	{
		if (Mode == BuildMode.Clear) return Clear;
		if (Mode == BuildMode.Parallel) return Parallel;
		if (Mode == BuildMode.Turn) return Turn;
		return Build;
	}

	public void BuildPressed()
	{
		if (Active) Tool().Pressed();
	}

	public void BuildReleased()
	{
		if (Active) Tool().Released();
	}

	public void Back()
	{
		Cancel();
	}

	public void Cancel()
	{
		if (Build == null) return;
		while (Tool().Cancel()) { }
		Snapper.Kind = GuideKind.None;
	}

	public void Escape()
	{
		if (!Active) return;
		if (!Tool().Cancel() && Mode != BuildMode.Build) SetMode(BuildMode.Build);
	}

	public void ToggleClear()
	{
		if (Active) SetMode(Mode == BuildMode.Clear ? BuildMode.Build : BuildMode.Clear);
	}

	public void ToggleParallel()
	{
		if (Active) SetMode(Mode == BuildMode.Parallel ? BuildMode.Build : BuildMode.Parallel);
	}

	public void ToggleTurns()
	{
		if (Active) SetMode(Mode == BuildMode.Turn ? BuildMode.Build : BuildMode.Turn);
	}

	public void ResetTurns()
	{
		if (Active) Turn.Reset();
	}

	public void StepBack()
	{
		if (Active && Build.Placing && Build.Chain > 0) Undo();
	}

	public void ToggleLevel()
	{
		if (!Active) return;
		Cancel();
		Level = Level == 0 ? 1 : 0;
	}

	public void ToggleDebug()
	{
		if (!Active) return;
		ShowDebug = !ShowDebug;
		Renderer.ShowDebug = ShowDebug;
		Renderer.QueueRedraw();
	}

	public void ChangeLanes(int delta)
	{
		LanesPerDirection = Mathf.Clamp(LanesPerDirection + delta, 1, MaxLanes);
	}

	private void SetMode(BuildMode mode)
	{
		Cancel();
		Mode = mode;
	}

	public float CostOf(Curve2D curve, int lanes, bool oneWay)
	{
		return curve.GetBakedLength() * CostPerPixel * lanes * (oneWay ? 0.5f : 1f) * (Level > 0 ? BridgeCostMultiplier : 1f);
	}

	public bool BuildPaid(Curve2D curve, int lanes, bool oneWay, bool reverse, RoadSnap start, RoadSnap end, Vector2 dir, List<Vector2> candidates)
	{
		float cost = CostOf(curve, lanes, oneWay);
		if (Data.Balance < cost) return false;

		RoadEdit edit = reverse
			? Network.Build(RoadGeometry.Reverse(curve), lanes, LaneWidth, Level, oneWay, end, start)
			: Network.Build(curve, lanes, LaneWidth, Level, oneWay, start, end);
		if (edit == null) return false;

		Data.Balance -= cost;
		History.Add(edit, cost, start, dir, candidates ?? new List<Vector2>());
		Renderer.Grow(edit.Built);
		return true;
	}

	public void Undo()
	{
		if (!Active) return;
		RoadHistory.Entry e = History.Undo();
		if (e == null) return;
		Network.Undo(e.Edit);
		Data.Balance += e.Cost;
		if (Build.Placing && Build.Chain > 0) Build.RestoreAfterUndo(e);
		else Cancel();
	}

	public void Redo()
	{
		if (!Active) return;
		RoadHistory.Entry e = History.NextRedo();
		if (e == null) return;
		if (Data.Balance < e.Cost)
		{
			Deny();
			return;
		}
		History.Redo();
		Cancel();
		Network.Redo(e.Edit);
		Data.Balance -= e.Cost;
		Renderer.Grow(e.Edit.Built);
	}

	public void Deny()
	{
		Shake = 0.25f;
	}

	public Vector2 Grid(Vector2 p)
	{
		return new Vector2(Mathf.Round(p.X / GridSize), Mathf.Round(p.Y / GridSize)) * GridSize;
	}
}
