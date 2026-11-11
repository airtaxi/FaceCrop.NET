namespace FaceCrop.Detection;

/// <summary>
/// Weighted non-maximum suppression: each cluster of candidates overlapping the best remaining one
/// is replaced by their score-weighted average box and keypoints, keeping the best score.
/// </summary>
internal static class WeightedNonMaxSuppression
{
    internal static List<RawDetection> Apply(IReadOnlyList<RawDetection> detections, float suppressionThreshold)
    {
        var remaining = Enumerable.Range(0, detections.Count).OrderByDescending(index => detections[index].Score).ToList();
        var output = new List<RawDetection>();
        while (remaining.Count > 0)
        {
            var best = detections[remaining[0]];
            var cluster = new List<RawDetection>();
            var rest = new List<int>();
            foreach (var index in remaining)
            {
                if (IntersectionOverUnion(detections[index], best) > suppressionThreshold) cluster.Add(detections[index]);
                else rest.Add(index);
            }
            output.Add(cluster.Count == 0 ? best : Average(best, cluster));
            if (rest.Count == remaining.Count) break;
            remaining = rest;
        }
        return output;
    }

    private static RawDetection Average(RawDetection best, List<RawDetection> cluster)
    {
        var totalScore = 0f;
        var weightedXMin = 0f;
        var weightedYMin = 0f;
        var weightedXMax = 0f;
        var weightedYMax = 0f;
        var keypoints = new float[best.Keypoints.Length];
        foreach (var candidate in cluster)
        {
            totalScore += candidate.Score;
            weightedXMin += candidate.XMin * candidate.Score;
            weightedYMin += candidate.YMin * candidate.Score;
            weightedXMax += candidate.XMax * candidate.Score;
            weightedYMax += candidate.YMax * candidate.Score;
            for (var index = 0; index < keypoints.Length; index++) keypoints[index] += candidate.Keypoints[index] * candidate.Score;
        }
        var xMin = weightedXMin / totalScore;
        var yMin = weightedYMin / totalScore;
        for (var index = 0; index < keypoints.Length; index++) keypoints[index] /= totalScore;
        return new RawDetection(xMin, yMin, weightedXMax / totalScore - xMin, weightedYMax / totalScore - yMin, keypoints, best.Score);
    }

    private static float IntersectionOverUnion(RawDetection first, RawDetection second)
    {
        if (IsEmpty(first) || IsEmpty(second) || second.XMax < first.XMin || first.XMax < second.XMin || second.YMax < first.YMin || first.YMax < second.YMin) return 0f;
        var intersectionWidth = Math.Min(first.XMax, second.XMax) - Math.Max(first.XMin, second.XMin);
        var intersectionHeight = Math.Min(first.YMax, second.YMax) - Math.Max(first.YMin, second.YMin);
        var intersectionArea = intersectionWidth * intersectionHeight;
        var normalization = Area(first) + Area(second) - intersectionArea;
        return normalization > 0f ? intersectionArea / normalization : 0f;
    }

    private static bool IsEmpty(RawDetection detection) => detection.XMin > detection.XMax || detection.YMin > detection.YMax;

    private static float Area(RawDetection detection) => (detection.XMax - detection.XMin) * (detection.YMax - detection.YMin);
}
