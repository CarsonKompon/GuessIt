using Sandbox;
using Sandbox.UI;
using System;
using System.Collections.Generic;
using System.Reflection.Metadata.Ecma335;

namespace GuessIt;

public partial class GameCanvas
{
    // Drawing Variables
    bool IsDrawing = false;
    List<Vector2> DrawingPoints = new List<Vector2>();
    Color PrimaryColor = Color.Black;
    Color SecondaryColor = Color.White;
    Color BrushColor = Color.Black;
    int BrushSize = 3;

    // UI Variables
    Image Canvas { get; set; }

    public void SetTexture(Texture texture)
    {
        Canvas.Texture = texture;
    }

    protected override void OnMouseDown(MousePanelEvent e)
    {
        base.OnClick(e);

        if(e.MouseButton == MouseButtons.Right)
        {
            BrushColor = SecondaryColor;
        }
        else
        {
            BrushColor = PrimaryColor;
        }

        DrawingPoints.Clear();
        AddPoint(e.LocalPosition);
        IsDrawing = true;
    }

	protected override void OnMouseMove( MousePanelEvent e )
	{
		base.OnMouseMove( e );

        if (IsDrawing)
        {
            bool returnVal = AddPoint(e.LocalPosition);
            if(returnVal == false)
            {
                StopDrawing();
            }
        }
        else if(e.MouseButton == MouseButtons.Left || e.MouseButton == MouseButtons.Right)
        {

        }
	}

    protected override void OnMouseUp(MousePanelEvent e)
    {
        base.OnMouseUp(e);

        StopDrawing();
    }

    void StopDrawing()
    {
        IsDrawing = false;
        if(DrawingPoints.Count > 0)
        {
            GuessIt.Instance.GameMenu.NetworkDraw(DrawingPoints, BrushColor, BrushSize);
        }
        DrawingPoints.Clear();
    }

	bool AddPoint(Vector2 vec2)
    {
        if(!IsDrawing) return false;
        Vector2 pos = (vec2 / Canvas.Box.Rect.Size) * new Vector2(320, 240);
        if(pos.x < 0 || pos.x > 320 || pos.y < 0 || pos.y > 240) return false;
        DrawingPoints.Add(pos);

        if(DrawingPoints.Count > 1)
        {
            List<Vector2> points = new List<Vector2>();
            points.Add(DrawingPoints[DrawingPoints.Count - 2]);
            points.Add(DrawingPoints[DrawingPoints.Count - 1]);
            GuessIt.Instance.GameMenu.Draw(points, BrushColor, BrushSize);
        }
        else
        {
            GuessIt.Instance.GameMenu.Draw(DrawingPoints[0], BrushColor, BrushSize);
        }

        return true;
    }
}