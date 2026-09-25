namespace SnapLingo.Domain.Enums;

/// <summary>Which memory the story generator may draw on (proposal experiment E2).</summary>
public enum MemoryMode
{
    /// <summary>Current photo and its metadata only: the original SnapLingo behavior.</summary>
    None,

    /// <summary>Plus related past events, habits and context changes from this learner's memory.</summary>
    Personal
}

/// <summary>How story claims are verified before display (proposal experiment E3).</summary>
public enum VerifierMode
{
    /// <summary>No verification: the story is shown as generated.</summary>
    None,

    /// <summary>Claims may only be supported by the current photo (baseline verifier).</summary>
    ImageOnly,

    /// <summary>Claims may be supported by the photo, memory, metadata or person labels.</summary>
    MultiSource
}

/// <summary>Verdict for one atomic claim (proposal section 5.4).</summary>
public enum ClaimLabel
{
    Grounded,
    Inferred,
    Unsupported
}

/// <summary>Where a piece of evidence comes from (proposal section 5.4).</summary>
public enum EvidenceSource
{
    Image,
    Memory,
    Metadata,
    Person
}
