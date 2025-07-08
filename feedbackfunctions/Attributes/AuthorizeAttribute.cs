using System;

namespace FeedbackFunctions.Attributes;

/// <summary>
/// Attribute to mark functions that require authentication
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class AuthorizeAttribute : Attribute
{
    /// <summary>
    /// Whether to require authentication for this function
    /// </summary>
    public bool Required { get; set; } = true;

    /// <summary>
    /// Constructor for authorization attribute
    /// </summary>
    /// <param name="required">Whether authentication is required</param>
    public AuthorizeAttribute(bool required = true)
    {
        Required = required;
    }
}