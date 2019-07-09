namespace LicencingNET
{
    /// <summary>
    /// Validation result.
    /// </summary>
    public enum ValidationResult
    {
        /// <summary>
        /// No failure. The licence is valid.
        /// </summary>
        Valid,
        /// <summary>
        /// The licence has expired.
        /// </summary>
        Exipired,
        /// <summary>
        /// The licence validity period has not yet started.
        /// </summary>
        NotStarted,
        /// <summary>
        /// The signature did not match. 
        /// The licence might have been altered or is forged.
        /// </summary>
        InvalidSignature,
        /// <summary>
        /// The licence has no signature.
        /// </summary>
        NoSignature
    }
}
