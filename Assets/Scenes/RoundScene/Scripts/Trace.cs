using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using Unity.VisualScripting;

[RequireComponent(typeof(LineRenderer))]
[RequireComponent(typeof(EdgeCollider2D))]
public class Trace : MonoBehaviour
{

    List<LineRenderer> recentLineRenderer = new();
    List<EdgeCollider2D> recentEdgeCollider = new();

    LineRenderer currentLine;
    EdgeCollider2D currentEdgeCollider;

    public Transform cursor;

    List<Vector2> points;

    public float pointSpacing = .5f;

    float randomGapTime = 0f;
    float gapSize = 0.5f;
    float nextGapAt = 0;

    bool addPoints = true;

    Vector3 lastPosition1;
    Vector3 lastPosition2;

    // Start is called before the first frame update
    void Start()
    {
        randomGapTime = Random.Range(3.0f, 5.0f);

        currentLine = GetComponent<LineRenderer>();
        currentEdgeCollider = GetComponent<EdgeCollider2D>();
        points = new List<Vector2>();
        SetPoint();

        nextGapAt = Time.time + randomGapTime;
    }

    // Update is called once per frame
    void Update()
    {
        if (addPoints) {
            if (Vector3.Distance(points.Last(), cursor.position) > pointSpacing) {
                SetPoint();
            }
        }
    }

    void SetPoint() {

        if (points.Count > 2) {
            // omit newest point to avoid detected collision with cursor
            currentEdgeCollider.points = points.GetRange(0, points.Count - 2).ToArray<Vector2>();
        }

        points.Add(cursor.position);
        currentLine.positionCount = points.Count;
        currentLine.SetPosition(points.Count - 1, cursor.position);
        
    }

    public void EndTrace() {
        addPoints = false;
    }

    void CreateGap() {
        if (Time.time > nextGapAt) {
            addPoints = false;
            if (Time.time > GapEndTime()) {
                Debug.Log($"Gap end because time {Time.time} > gapEndTime {GapEndTime()}");
                recentLineRenderer.Add(currentLine);
                recentEdgeCollider.Add(currentEdgeCollider);

                currentLine = gameObject.AddComponent<LineRenderer>();
                currentEdgeCollider = gameObject.AddComponent<EdgeCollider2D>();

                nextGapAt = GenerateNextGapStartTime();
                addPoints = true;
            }
        }
    }

    float GenerateNextGapStartTime() {
        return Time.time + Random.Range(3.0f, 5.0f);
    }

    float GapEndTime() {
        return nextGapAt + gapSize;
    }

}
