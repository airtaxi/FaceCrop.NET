namespace FaceCrop;

/// <summary>
/// A face dropped during low-confidence recovery.
/// </summary>
/// <param name="Score">The face's score.</param>
/// <param name="GapFromBest">Difference between the best score and this score.</param>
/// <param name="Box">The face box in source image pixels.</param>
/// <param name="SupportWindows">Number of detection windows that found the face.</param>
/// <param name="LowerPositionPriority">The face sits well below an upper anchor face and was removed first.</param>
/// <param name="UpperCandidateRemoved">The face was in the upper 45% of the image or above the anchor face.</param>
public sealed record RemovedCandidate(double Score, double GapFromBest, FaceBox Box, int SupportWindows, bool LowerPositionPriority, bool UpperCandidateRemoved);
