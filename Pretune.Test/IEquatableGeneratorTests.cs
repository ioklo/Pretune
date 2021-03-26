using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using static Pretune.Test.TestMisc;

namespace Pretune.Test
{
    public class IEquatableGeneratorTests
    {
        // UnitOfWorkName_ScenarioName_ExpectedBehavior

        [Fact]
        public void ImplementIEquatable_SimpleClass_ImplementIEquatable()
        {
            var input = @"
namespace N
{
    [ImplementIEquatable]
    public partial class Script
    {
        int x;
        public string Y { get; }
    }
}";
            var output = SingleTextProcess(input);
            var expected = @"#nullable enable

namespace N
{
    public partial class Script : System.IEquatable<Script>
    {
        public override bool Equals(object? obj) => Equals(obj as Script);
        public bool Equals(Script? other)
        {
            if (other == null)
                return false;
            if (!x.Equals(other.x))
                return false;
            if (!Y.Equals(other.Y))
                return false;
            return true;
        }

        public override int GetHashCode()
        {
            var hashCode = new System.HashCode();
            hashCode.Add(this.x.GetHashCode());
            hashCode.Add(this.Y.GetHashCode());
            return hashCode.ToHashCode();
        }
    }
}";

            Assert.Equal(expected, output);
        }

        [Fact]
        public void ImplementIEquatable_SimpleStruct_ImplementIEquatable()
        {
            var input = @"
namespace N
{
    [ImplementIEquatable]
    public partial struct Script
    {
        int x;
        public string Y { get; }
    }
}";
            var output = SingleTextProcess(input);
            var expected = @"#nullable enable

namespace N
{
    public partial struct Script : System.IEquatable<Script>
    {
        public override bool Equals(object? obj) => obj is Script other && Equals(other);
        public bool Equals(Script other)
        {
            if (!x.Equals(other.x))
                return false;
            if (!Y.Equals(other.Y))
                return false;
            return true;
        }

        public override int GetHashCode()
        {
            var hashCode = new System.HashCode();
            hashCode.Add(this.x.GetHashCode());
            hashCode.Add(this.Y.GetHashCode());
            return hashCode.ToHashCode();
        }
    }
}";

            Assert.Equal(expected, output);
        }

        [Fact]
        public void ImplementIEquatable_HasNullableStructMember_ImplementIEquatable()
        {
            var input = @"
namespace N
{
    [ImplementIEquatable]
    public partial class Script
    {
        int? nx;
    }
}";
            var output = SingleTextProcess(input);
            var expected = @"#nullable enable

namespace N
{
    public partial class Script : System.IEquatable<Script>
    {
        public override bool Equals(object? obj) => Equals(obj as Script);
        public bool Equals(Script? other)
        {
            if (other == null)
                return false;
            if (nx != null && other.nx != null)
            {
                if (!nx.Value.Equals(other.nx.Value))
                    return false;
            }
            else if (nx != null || other.nx != null)
                return false;
            return true;
        }

        public override int GetHashCode()
        {
            var hashCode = new System.HashCode();
            hashCode.Add(this.nx == null ? 0 : this.nx.Value.GetHashCode());
            return hashCode.ToHashCode();
        }
    }
}";

            Assert.Equal(expected, output);
        }

        [Fact]
        public void ImplementIEquatable_HasNullableClass_ImplementIEquatable()
        {
            var input = @"
namespace N
{
    [ImplementIEquatable]
    public partial class Script
    {
        string? ns;
    }
}";
            var output = SingleTextProcess(input);
            var expected = @"#nullable enable

namespace N
{
    public partial class Script : System.IEquatable<Script>
    {
        public override bool Equals(object? obj) => Equals(obj as Script);
        public bool Equals(Script? other)
        {
            if (other == null)
                return false;
            if (ns != null)
            {
                if (!ns.Equals(other.ns))
                    return false;
            }
            else if (other.ns != null)
                return false;
            return true;
        }

        public override int GetHashCode()
        {
            var hashCode = new System.HashCode();
            hashCode.Add(this.ns == null ? 0 : this.ns.GetHashCode());
            return hashCode.ToHashCode();
        }
    }
}";
            Assert.Equal(expected, output);
        }

        [Fact]
        public void ImplementIEquatable_HasCustomEquatableMember_UsingCustomEqualityComparer()
        {
            var input = @"

using System.Collections.Immutable;

namespace N
{
    [ImplementIEquatable]
    public partial class Script
    {
        ImmutableArray<int> ns;
        ImmutableArray<int>? ns2;
    }
}";
            var output = SingleTextProcess(input);
            var expected = @"#nullable enable

using System.Collections.Immutable;

namespace N
{
    public partial class Script : System.IEquatable<Script>
    {
        public override bool Equals(object? obj) => Equals(obj as Script);
        public bool Equals(Script? other)
        {
            if (other == null)
                return false;
            if (!global::Pretune.DefaultCustomEqualityComparer.Equals(ns, other.ns))
                return false;
            if (ns2 != null)
            {
                if (!global::Pretune.DefaultCustomEqualityComparer.Equals(ns2, other.ns2))
                    return false;
            }
            else if (other.ns2 != null)
                return false;
            return true;
        }

        public override int GetHashCode()
        {
            var hashCode = new System.HashCode();
            hashCode.Add(global::Pretune.DefaultCustomEqualityComparer.GetHashCode(this.ns));
            hashCode.Add(this.ns2 == null ? 0 : global::Pretune.DefaultCustomEqualityComparer.GetHashCode(this.ns2));
            return hashCode.ToHashCode();
        }
    }
}";
            Assert.Equal(expected, output);
        }
    }
}
