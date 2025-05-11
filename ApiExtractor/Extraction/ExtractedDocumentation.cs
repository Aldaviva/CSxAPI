using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace ApiExtractor.Extraction;

public class ExtractedDocumentation {

    public ICollection<DocXCommand> Commands { get; set; } = new List<DocXCommand>();
    public ICollection<DocXConfiguration> Configurations { get; set; } = new List<DocXConfiguration>();
    public ICollection<DocXStatus> Statuses { get; set; } = new List<DocXStatus>();
    public ICollection<DocXEvent> Events { get; set; } = new List<DocXEvent>();

}

public interface IPathNamed {

    public IList<string> Name { get; init; }
    public IList<string> NameWithoutBrackets { get; }

}

public abstract class AbstractCommand: IPathNamed {

    public IList<string> Name { get; init; } = new List<string>();
    public virtual IList<string> NameWithoutBrackets => Name;
    public ISet<Product> AppliesTo { get; set; } = new HashSet<Product>();
    public ISet<UserRole> RequiresUserRole { get; set; } = new HashSet<UserRole>();
    public string Description { get; set; } = string.Empty;

    public override string ToString() {
        return string.Join(" ", Name);
    }

}

public enum UserRole {

    Admin,
    Integrator,
    User,
    Audit,
    Roomcontrol,
    Touchuser,
    Paireduser

}

[SuppressMessage("ReSharper", "InconsistentNaming")]
public enum Product {

    Board,
    BoardPro,
    BoardProG2,
    CodecEQ,
    CodecPlus,
    CodecPro,
    DeskPro,
    DeskMini,
    Desk,
    Room55,
    Room70,
    Room55D,
    Room70G2,
    RoomBar,
    RoomBarPro,
    RoomKit,
    RoomKitEQX,
    RoomKitMini,
    RoomPanorama,
    Room70Panorama

}

public class DocXConfiguration: AbstractCommand {

    public IList<Parameter> Parameters { get; set; } = new List<Parameter>();

    public override IList<string> NameWithoutBrackets =>
        // name.Where((s, i) => !parameters.Any(parameter => parameter is IntParameter { indexOfParameterInName: { } paramIndex } && paramIndex == i)).ToList();
        Name.Select((s, i) => Parameters.Any(parameter => parameter is IntParameter { IndexOfParameterInName: { } paramIndex } && paramIndex == i) ? "N" : s).ToList();

}

public abstract class Parameter {

    public string Name { get; set; } = null!;
    public string Description { get; set; } = string.Empty;
    public string? ValueSpaceDescription { get; set; }
    public bool Required { get; set; }
    public string? DefaultValue { get; set; }
    public ISet<Product>? AppliesTo { get; set; }

    public abstract DataType Type { get; }

}

public enum DataType {

    Integer,
    String,
    Enum

}

public class IntParameter: Parameter {

    public int? IndexOfParameterInName { get; set; }
    public IList<IntRange> Ranges { get; set; } = new List<IntRange>();
    public override DataType Type => DataType.Integer;
    public string? NamePrefix { get; set; }

}

internal class EnumParameter: Parameter, IEnumValues {

    public ISet<EnumValue> PossibleValues { get; set; } = null!;
    public override DataType Type => DataType.Enum;

}

public class EnumValue {

    public EnumValue(string name) {
        Name = name;
    }

    public string Name { get; set; }
    public string? Description { get; set; }

    protected bool Equals(EnumValue other) {
        return string.Equals(Name, other.Name, StringComparison.InvariantCultureIgnoreCase);
    }

    public override bool Equals(object? obj) {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((EnumValue) obj);
    }

    public override int GetHashCode() {
        return StringComparer.InvariantCultureIgnoreCase.GetHashCode(Name);
    }

    public static bool operator ==(EnumValue? left, EnumValue? right) {
        return Equals(left, right);
    }

    public static bool operator !=(EnumValue? left, EnumValue? right) {
        return !Equals(left, right);
    }

}

internal class StringParameter: Parameter {

    public int MinimumLength { get; set; }
    public int MaximumLength { get; set; }
    public override DataType Type => DataType.String;

}

public class DocXCommand: DocXConfiguration { }

public class DocXStatus: AbstractCommand {

    public IList<IntParameter> ArrayIndexParameters { get; } = new List<IntParameter>();
    public ValueSpace ReturnValueSpace { get; set; } = null!;

    public override IList<string> NameWithoutBrackets =>
        // name.Where((s, i) => !arrayIndexParameters.Any(parameter => parameter is { indexOfParameterInName: { } paramIndex } && paramIndex == i)).ToList();
        Name.Select((s, i) => ArrayIndexParameters.Any(parameter => parameter is { IndexOfParameterInName: { } paramIndex } && paramIndex == i) ? "N" : s).ToList();

}

public abstract class ValueSpace {

    public abstract DataType Type { get; }
    public string? Description { get; set; }

}

internal class IntValueSpace: ValueSpace {

    public IList<IntRange> Ranges = new List<IntRange>();
    public override DataType Type => DataType.Integer;

    /// <summary>
    /// How the null value for this int valuespace is serialized.
    /// For example, xStatus Network [n] VLAN Voice VlanId returns an integer in the range [1, 4094], or the string "Off" if the VLAN Voice Mode is not enabled.
    /// If set, this string will get translated to null when reading this status. In most cases, this property should be null.
    /// </summary>
    public string? OptionalValue { get; set; }

}

internal interface IEnumValues {

    ISet<EnumValue> PossibleValues { get; set; }

}

internal class EnumValueSpace: ValueSpace, IEnumValues {

    public ISet<EnumValue> PossibleValues { get; set; } = null!;
    public override DataType Type => DataType.Enum;

}

internal class StringValueSpace: ValueSpace {

    public override DataType Type => DataType.String;

}

public class IntRange {

    public int Minimum { get; set; }
    public int Maximum { get; set; }
    public string? Description { get; set; }
    public ISet<Product> AppliesTo { get; set; } = new HashSet<Product>();

}

public class DocXEvent: IEventParent, IPathNamed {

    public IList<string> Name { get; init; } = new List<string>();
    public IList<string> NameWithoutBrackets => Name;

    // TODO parameters are actually not just a flat list, they can be arbitrarily nested (up to 6 layers deep in practice)
    // see "csxapi todo.txt"
    // This is a problem because not only do we have to generate a very large graph of objects at compile time and again at runtime,
    // but we also must handle numeric indices in the result path somehow, which aren't 0-indexed and are sparse, so we can't just use a List
    // Maybe an IDictionary<int, object> would be better, because then consumers can use Indexer accessors or get all the entries if they want to enumerate them
    // public ICollection<Parameter> parameters { get; set; } = new List<Parameter>();

    public IList<EventChild> Children { get; set; } = new List<EventChild>();

    public ISet<UserRole> RequiresUserRole { get; set; } = new HashSet<UserRole>();
    public EventAccess Access { get; set; }

}

public enum EventAccess {

    PublicAPI,
    PublicAPIPreview,
    Internal,
    InternalRestricted

}

public static class EventAccessParser {

    public static EventAccess Parse(string serialized) => serialized.ToLowerInvariant() switch {
        "public-api"          => EventAccess.PublicAPI,
        "public-api-preview"  => EventAccess.PublicAPIPreview,
        "internal"            => EventAccess.Internal,
        "internal-restricted" => EventAccess.InternalRestricted,
    };

}

public abstract class EventChild: IPathNamed {

    public IList<string> Name { get; init; } = null!;
    public IList<string> NameWithoutBrackets => Name;

}

public interface IEventParent: IPathNamed {

    IList<EventChild> Children { get; set; }

}

public class ListContainer: EventChild, IEventParent {

    public IList<EventChild> Children { get; set; } = new List<EventChild>();

}

public class ObjectContainer: EventChild, IEventParent {

    public IList<EventChild> Children { get; set; } = new List<EventChild>();
    public bool Required { get; set; } = true;

}

public abstract class ValueChild: EventChild {

    public abstract DataType Type { get; }
    public abstract bool Required { get; set; }

}

public class StringChild: ValueChild {

    public override DataType Type => DataType.String;
    public override bool Required { get; set; }

}

public class IntChild: ValueChild {

    public override DataType Type => DataType.Integer;
    public override bool Required { get; set; }

    /// <summary>
    /// This is not a named property inside an event.
    /// Instead, the entire event body is just this value. For example,
    ///
    /// <c>
    /// {
    ///     "Standby": {
    ///         "SecondsToStandby": 30,
    ///         "id": 1
    ///     }
    /// }
    /// </c>
    /// Used by <c>Standby/SecondsToStandby</c> and <c>RoomReset/SecondsToReset</c>.
    /// </summary>
    public bool ImplicitAnonymousSingleton { get; set; } = false;

}

public class EnumChild: ValueChild {

    public override DataType Type => DataType.Enum;
    public override bool Required { get; set; }
    public ISet<EnumValue> PossibleValues { get; set; } = null!;

}