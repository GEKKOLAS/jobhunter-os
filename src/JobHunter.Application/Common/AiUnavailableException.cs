namespace JobHunter.Application.Common;

/// <summary>
/// A model failed, declined, or answered something its checks rejected. Every AI feature has a
/// non-AI path, so this never reaches the user as an error: the rules or a blank draft take over.
/// </summary>
public sealed class AiUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
