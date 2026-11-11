namespace FaceCrop;

/// <summary>
/// An image could not be cropped. The message is a short machine-readable reason, optionally followed by details.
/// </summary>
/// <param name="message">The failure reason.</param>
public class FaceCropException(string message) : Exception(message);
