using HydraForge.Domain.Common;

namespace HydraForge.Domain.Tests.Common;

public class DomainErrorCodesTests
{
    [Fact]
    public void Projects_NotFound_HasCorrectCode()
    {
        Assert.Equal("PROJECT_NOT_FOUND", DomainErrorCodes.Projects.NotFound);
    }

    [Fact]
    public void Projects_Archived_HasCorrectCode()
    {
        Assert.Equal("PROJECT_ARCHIVED", DomainErrorCodes.Projects.Archived);
    }

    [Fact]
    public void Projects_OwnerRequired_HasCorrectCode()
    {
        Assert.Equal("PROJECT_OWNER_REQUIRED", DomainErrorCodes.Projects.OwnerRequired);
    }

    [Fact]
    public void Projects_LastOwnerRemovalDenied_HasCorrectCode()
    {
        Assert.Equal("PROJECT_LAST_OWNER_REMOVAL_DENIED", DomainErrorCodes.Projects.LastOwnerRemovalDenied);
    }

    [Fact]
    public void Projects_MembershipDenied_HasCorrectCode()
    {
        Assert.Equal("PROJECT_MEMBERSHIP_DENIED", DomainErrorCodes.Projects.MembershipDenied);
    }

    [Fact]
    public void Projects_MemberDuplicate_HasCorrectCode()
    {
        Assert.Equal("PROJECT_MEMBER_DUPLICATE", DomainErrorCodes.Projects.MemberDuplicate);
    }
}