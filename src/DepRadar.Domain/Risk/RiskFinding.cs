namespace DepRadar.Domain.Risk;

/// <summary>
/// A single, explainable reason a package carries risk. Findings are what make the
/// health score auditable rather than a black-box number.
/// </summary>
/// <param name="Category">Which health dimension this concerns.</param>
/// <param name="Level">How severe it is.</param>
/// <param name="Code">A short stable code, e.g. <c>VULN</c>, <c>COPYLEFT</c>, <c>LICENSE_SHIFT</c>.</param>
/// <param name="Message">A human-readable explanation.</param>
public sealed record RiskFinding(RiskCategory Category, RiskLevel Level, string Code, string Message);
