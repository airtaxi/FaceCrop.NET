namespace FaceCrop.Detection;

/// <summary>
/// One face found by <see cref="BlazeFaceDetector"/>.
/// </summary>
/// <param name="BoundingBox">Face box in pixels of the image passed to the detector.</param>
/// <param name="Keypoints">
/// Six normalized keypoints in model order: right eye, left eye, nose tip, mouth center, right ear tragion and left ear tragion.
/// </param>
/// <param name="Score">Detection confidence from 0 to 1.</param>
public sealed record FaceDetection(DetectionBox BoundingBox, IReadOnlyList<NormalizedPoint> Keypoints, float Score);
