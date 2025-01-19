using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace ApiExtractor.Extraction;

public class EventReader {

    private readonly ExtractedDocumentation _docs;

    public EventReader(ExtractedDocumentation docs) {
        _docs = docs;
    }

    public void ParseEventXml(string xmlFilename) {
        XDocument doc = XDocument.Load(xmlFilename);

        foreach (XElement secondLevelElements in doc.Root!.Elements()) {
            Visit(secondLevelElements, new[] { "xEvent" });
        }

        Console.WriteLine($"Parsed {_docs.Events.Count:N0} xEvents from XML");
    }

    private void Visit(XElement el, IList<string> path) {
        path = path.Append(el.Name.LocalName).ToList();

        if (AttributeEquals(el, "event", "True")) {
            DocXEvent xEvent = new() {
                Name             = path,
                Access           = EventAccessParser.Parse(el.Attribute("access")!.Value),
                RequiresUserRole = (el.Attribute("role")?.Value.Split(";").Select(rawRole => Enum.Parse<UserRole>(rawRole, true)) ?? Enumerable.Empty<UserRole>()).ToHashSet(),
            };

            if (xEvent.Access == EventAccess.PublicAPI) {
                _docs.Events.Add(xEvent);

                foreach (XElement childEl in el.Elements()) {
                    Visit(childEl, xEvent);
                }
            }
        } else {
            foreach (XElement childEl in el.Elements()) {
                Visit(childEl, path);
            }
        }
    }

    private static void Visit(XElement el, IEventParent parent) {
        IList<string> name     = parent.Name.Append(el.Attribute("className")?.Value ?? el.Name.LocalName).ToList();
        bool          required = !AttributeEquals(el, "optional", "True");

        if (AttributeEquals(el, "type", "literal") && el.HasElements) {
            parent.Children.Add(new EnumChild {
                Name           = name,
                Required       = required,
                PossibleValues = el.Elements("Value").Select(valueEl => new EnumValue(valueEl.Value)).ToHashSet()
            });
        } else if (AttributeEquals(el, "type", "string") || (AttributeEquals(el, "type", "literal") && !el.HasElements)) {
            parent.Children.Add(new StringChild {
                Name     = name,
                Required = required
            });
        } else if (AttributeEquals(el, "type", "int")) {
            parent.Children.Add(new IntChild {
                Name                       = name,
                Required                   = required,
                ImplicitAnonymousSingleton = AttributeEquals(el, "onlyTextNode", "true")
            });
        } else if (AttributeEquals(el, "multiple", "True")) {
            ListContainer listContainer = new() { Name = name };
            parent.Children.Add(listContainer);

            foreach (XElement childEl in el.Elements()) {
                Visit(childEl, listContainer);
            }
        } else /*if (attributeEquals(el, "basenode", "True"))*/ {
            ObjectContainer objectContainer = new() {
                Name     = name,
                Required = required
            };
            parent.Children.Add(objectContainer);

            foreach (XElement childEl in el.Elements()) {
                Visit(childEl, objectContainer);
            }
        }
    }

    private static bool AttributeEquals(XElement el, string attributeName, string? comparisonValue) {
        return string.Equals(el.Attribute(attributeName)?.Value, comparisonValue, StringComparison.InvariantCultureIgnoreCase);
    }

}