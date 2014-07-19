using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Collections.ObjectModel;
using System.Xml;

namespace yamlconv
{
    public enum ByamlNodeType
    {
        String = 0xa0,
        Data = 0xa1,
        Boolean = 0xd0,
        Int = 0xd1,
        Single = 0xd2,
        UnamedNode = 0xc0,
        NamedNode = 0xc1,
        StringList = 0xc2,
        BinaryDataList = 0xc3,
    }

    abstract class ByamlNode
    {
        public long Address { get; set; }
        public long Length { get; set; }

        public abstract ByamlNodeType Type { get; }
        public virtual bool CanBeAttribute { get { return false; } }

        public virtual void ToXml(XmlDocument yaml, XmlNode node, List<string> nodes, List<string> values, List<byte[]> data)
        {
            throw new NotImplementedException();
        }

        public class String : ByamlNode
        {
            public int Value { get; set; }

            public override ByamlNodeType Type
            {
                get { return ByamlNodeType.String; }
            }

            public String(EndianBinaryReader reader)
            {
                Address = reader.BaseStream.Position;

                Value = reader.ReadInt32();

                Length = reader.BaseStream.Position - Length;
            }

            public String(int value)
            {
                Value = value;
            }

            public override void ToXml(XmlDocument yaml, XmlNode node, List<string> nodes, List<string> values, List<byte[]> data)
            {
                XmlAttribute attr = yaml.CreateAttribute("type");
                attr.Value = "string";
                node.Attributes.Append(attr);
                node.InnerText = values[Value];
            }
        }

        public class Data : ByamlNode
        {
            public int Value { get; set; }

            public override ByamlNodeType Type
            {
                get { return ByamlNodeType.Data; }
            }

            public Data(EndianBinaryReader reader)
            {
                Address = reader.BaseStream.Position;

                Value = reader.ReadInt32();

                Length = reader.BaseStream.Position - Length;
            }

            public Data(int value)
            {
                Value = value;
            }

            public override void ToXml(XmlDocument yaml, XmlNode node, List<string> nodes, List<string> values, List<byte[]> data)
            {
                XmlAttribute attr = yaml.CreateAttribute("type");
                attr.Value = "path";
                node.Attributes.Append(attr);
                using (EndianBinaryReader rd = new EndianBinaryReader(new MemoryStream(data[Value])))
                {
                    while (rd.BaseStream.Position != rd.BaseStream.Length)
                    {
                        XmlElement point = yaml.CreateElement("point");
                        point.SetAttribute("x", rd.ReadSingle().ToString() + "f");
                        point.SetAttribute("y", rd.ReadSingle().ToString() + "f");
                        point.SetAttribute("z", rd.ReadSingle().ToString() + "f");
                        point.SetAttribute("nx", rd.ReadSingle().ToString() + "f");
                        point.SetAttribute("ny", rd.ReadSingle().ToString() + "f");
                        point.SetAttribute("nz", rd.ReadSingle().ToString() + "f");
                        point.SetAttribute("val", rd.ReadInt32().ToString());
                        node.AppendChild(point);
                    }
                    rd.Close();
                }
            }
        }

        public class Boolean : ByamlNode
        {
            public bool Value { get; set; }

            public override ByamlNodeType Type
            {
                get { return ByamlNodeType.Boolean; }
            }
            public override bool CanBeAttribute
            {
                get { return true; }
            }

            public Boolean(EndianBinaryReader reader)
            {
                Address = reader.BaseStream.Position;

                Value = reader.ReadInt32() != 0;

                Length = reader.BaseStream.Position - Length;
            }

            public Boolean(bool value)
            {
                Value = value;
            }

            public override void ToXml(XmlDocument yaml, XmlNode node, List<string> nodes, List<string> values, List<byte[]> data)
            {
#if FALSE
                node.Attributes.Append(yaml.CreateAttribute("type"));
                node.Attributes["type"].Value = "bool";
#endif
                node.InnerText = Value.ToString().ToLower();
            }
        }

        public class Int : ByamlNode
        {
            public int Value { get; set; }

            public override ByamlNodeType Type
            {
                get { return ByamlNodeType.Int; }
            }
            public override bool CanBeAttribute
            {
                get { return true; }
            }

            public Int(EndianBinaryReader reader)
            {
                Address = reader.BaseStream.Position;

                Value = reader.ReadInt32();

                Length = reader.BaseStream.Position - Length;
            }

            public Int(int value)
            {
                Value = value;
            }

            public override void ToXml(XmlDocument yaml, XmlNode node, List<string> nodes, List<string> values, List<byte[]> data)
            {
#if FALSE
                node.Attributes.Append(yaml.CreateAttribute("type"));
                node.Attributes["type"].Value = "int";
#endif
                node.InnerText = Value.ToString();
            }
        }

        public class Single : ByamlNode
        {
            public float Value { get; set; }

            public override ByamlNodeType Type
            {
                get { return ByamlNodeType.Single; }
            }
            public override bool CanBeAttribute
            {
                get { return true; }
            }

            public Single(EndianBinaryReader reader)
            {
                Address = reader.BaseStream.Position;

                Value = reader.ReadSingle();

                Length = reader.BaseStream.Position - Length;
            }

            public Single(float value)
            {
                Value = value;
            }

            public override void ToXml(XmlDocument yaml, XmlNode node, List<string> nodes, List<string> values, List<byte[]> data)
            {
#if FALSE
                node.Attributes.Append(yaml.CreateAttribute("type"));
                node.Attributes["type"].Value = "float";
#endif
                node.InnerText = Value.ToString() + "f";
            }
        }

        public class UnamedNode : ByamlNode
        {
            public Collection<ByamlNode> Nodes { get; private set; }

            public override ByamlNodeType Type
            {
                get { return ByamlNodeType.UnamedNode; }
            }

            public UnamedNode(EndianBinaryReader reader)
            {
                Address = reader.BaseStream.Position;

                Nodes = new Collection<ByamlNode>();

                int count = reader.ReadInt32() & 0xffffff;
                byte[] types = reader.ReadBytes(count);

                while (reader.BaseStream.Position % 4 != 0)
                    reader.ReadByte();

                long start = reader.BaseStream.Position;

                for (int i = 0; i < count; i++)
                {
                    ByamlNodeType type = (ByamlNodeType)types[i];

                    switch (type)
                    {
                        case ByamlNodeType.String:
                            Nodes.Add(new String(reader));
                            break;
                        case ByamlNodeType.Data:
                            Nodes.Add(new Data(reader));
                            break;
                        case ByamlNodeType.Boolean:
                            Nodes.Add(new Boolean(reader));
                            break;
                        case ByamlNodeType.Int:
                            Nodes.Add(new Int(reader));
                            break;
                        case ByamlNodeType.Single:
                            Nodes.Add(new Single(reader));
                            break;
                        case ByamlNodeType.UnamedNode:
                            reader.BaseStream.Position = reader.ReadInt32();
                            Nodes.Add(new UnamedNode(reader));
                            break;
                        case ByamlNodeType.NamedNode:
                            reader.BaseStream.Position = reader.ReadInt32();
                            Nodes.Add(new NamedNode(reader));
                            break;
                        default:
                            throw new InvalidDataException();
                    }

                    reader.BaseStream.Position = start + (i + 1) * 4;
                }

                Length = reader.BaseStream.Position - Length;
            }

            public UnamedNode()
            {
                Nodes = new Collection<ByamlNode>();
            }

            public override void ToXml(XmlDocument yaml, XmlNode node, List<string> nodes, List<string> values, List<byte[]> data)
            {
                int i = 0;
                node.Attributes.Append(yaml.CreateAttribute("type"));
                node.Attributes["type"].Value = "array";
                foreach (var item in Nodes)
                {
                    XmlElement element = yaml.CreateElement("value");
                    item.ToXml(yaml, element, nodes, values, data);
                    node.AppendChild(element);
                    i++;
                }
            }
        }

        public class NamedNode : ByamlNode
        {
            public Collection<KeyValuePair<int, ByamlNode>> Nodes { get; private set; }

            public override ByamlNodeType Type
            {
                get { return ByamlNodeType.NamedNode; }
            }

            public NamedNode(EndianBinaryReader reader)
            {
                Address = reader.BaseStream.Position;

                Nodes = new Collection<KeyValuePair<int, ByamlNode>>();

                int count = reader.ReadInt32() & 0xffffff;

                for (int i = 0; i < count; i++)
                {
                    uint temp = reader.ReadUInt32();
                    int name = (int)(temp >> 8);
                    ByamlNodeType type = (ByamlNodeType)(byte)temp;

                    switch (type)
                    {
                        case ByamlNodeType.String:
                            Nodes.Add(new KeyValuePair<int, ByamlNode>(name, new String(reader)));
                            break;
                        case ByamlNodeType.Data:
                            Nodes.Add(new KeyValuePair<int, ByamlNode>(name, new Data(reader)));
                            break;
                        case ByamlNodeType.Boolean:
                            Nodes.Add(new KeyValuePair<int, ByamlNode>(name, new Boolean(reader)));
                            break;
                        case ByamlNodeType.Int:
                            Nodes.Add(new KeyValuePair<int, ByamlNode>(name, new Int(reader)));
                            break;
                        case ByamlNodeType.Single:
                            Nodes.Add(new KeyValuePair<int, ByamlNode>(name, new Single(reader)));
                            break;
                        case ByamlNodeType.UnamedNode:
                            reader.BaseStream.Position = reader.ReadInt32();
                            Nodes.Add(new KeyValuePair<int, ByamlNode>(name, new UnamedNode(reader)));
                            break;
                        case ByamlNodeType.NamedNode:
                            reader.BaseStream.Position = reader.ReadInt32();
                            Nodes.Add(new KeyValuePair<int, ByamlNode>(name, new NamedNode(reader)));
                            break;
                        default:
                            throw new InvalidDataException();
                    }

                    reader.BaseStream.Position = Address + (i + 1) * 8 + 4;
                }

                Length = reader.BaseStream.Position - Length;
            }

            public NamedNode()
            {
                Nodes = new Collection<KeyValuePair<int, ByamlNode>>();
            }

            public override void ToXml(XmlDocument yaml, XmlNode node, List<string> nodes, List<string> values, List<byte[]> data)
            {
#if FALSE
                if (Nodes.Count == 0)
                {
                    node.Attributes.Append(yaml.CreateAttribute("type"));
                    node.Attributes["type"].Value = "named";
                }
#endif
                foreach (var item in Nodes)
                {
                    if (item.Value.CanBeAttribute &&
                        !string.Equals(nodes[item.Key], "type", StringComparison.OrdinalIgnoreCase))
                    {
                        XmlAttribute element = yaml.CreateAttribute(nodes[item.Key]);
                        item.Value.ToXml(yaml, element, nodes, values, data);
                        node.Attributes.Append(element);
                    }
                    else
                    {
                        XmlElement element = yaml.CreateElement(nodes[item.Key]);
                        item.Value.ToXml(yaml, element, nodes, values, data);
                        node.AppendChild(element);
                    }
                }
            }
        }

        public class StringList : ByamlNode
        {
            public Collection<string> Strings { get; private set; }

            public override ByamlNodeType Type
            {
                get { return ByamlNodeType.StringList; }
            }

            public StringList(EndianBinaryReader reader)
            {
                Address = reader.BaseStream.Position;

                Strings = new Collection<string>();

                int count = reader.ReadInt32() & 0xffffff;
                int[] offsets = reader.ReadInt32s(count);

                foreach (var item in offsets)
                {
                    reader.BaseStream.Seek(Address + item, SeekOrigin.Begin);
                    Strings.Add(reader.ReadStringNT(Encoding.ASCII));
                }

                Length = reader.BaseStream.Position - Length;
            }

            public StringList()
            {
                Strings = new Collection<string>();
            }
        }

        public class BinaryDataList : ByamlNode
        {
            public Collection<byte[]> Data { get; private set; }

            public override ByamlNodeType Type
            {
                get { return ByamlNodeType.BinaryDataList; }
            }

            public BinaryDataList(EndianBinaryReader reader)
            {
                Address = reader.BaseStream.Position;

                Data = new Collection<byte[]>();

                int count = reader.ReadInt32() & 0xffffff;
                int[] offsets = reader.ReadInt32s(count + 1);

                for (int i = 0; i < count; i++)
                {
                    reader.BaseStream.Seek(Address + offsets[i], SeekOrigin.Begin);
                    Data.Add(reader.ReadBytes(offsets[i + 1] - offsets[i]));
                }

                Length = reader.BaseStream.Position - Length;
            }

            public BinaryDataList()
            {
                Data = new Collection<byte[]>();
            }
        }

        public static ByamlNode FromXml(XmlDocument doc, XmlNode xmlNode, List<string> nodes, List<string> values, List<string> data)
        {
            XmlNode child = xmlNode.FirstChild;
            while (child != null && child.NodeType == XmlNodeType.Comment)
                child = child.NextSibling;

            if (child == null || child.NodeType == XmlNodeType.Element)
            {
                if (xmlNode.Attributes["type"] != null && xmlNode.Attributes["type"].Value == "array")
                {
                    UnamedNode node = new UnamedNode();
                    foreach (XmlNode item in xmlNode.ChildNodes)
                        if (item.NodeType == XmlNodeType.Element)
                            node.Nodes.Add(FromXml(doc, item, nodes, values, data));
                    return node;
                }
                else if (xmlNode.Attributes["type"] != null && xmlNode.Attributes["type"].Value == "path")
                {
                    string value;
                    using (MemoryStream ms = new MemoryStream())
                    {
                        using (EndianBinaryWriter wr = new EndianBinaryWriter(ms))
                        {
                            foreach (XmlNode item in xmlNode.ChildNodes)
                            {
                                if (item.NodeType == XmlNodeType.Element && string.Equals(item.Name, "point", StringComparison.OrdinalIgnoreCase))
                                {
                                    wr.Write(float.Parse(item.Attributes["x"].Value.Remove(item.Attributes["x"].Value.Length - 1)));
                                    wr.Write(float.Parse(item.Attributes["y"].Value.Remove(item.Attributes["y"].Value.Length - 1)));
                                    wr.Write(float.Parse(item.Attributes["z"].Value.Remove(item.Attributes["z"].Value.Length - 1)));
                                    wr.Write(float.Parse(item.Attributes["nx"].Value.Remove(item.Attributes["nx"].Value.Length - 1)));
                                    wr.Write(float.Parse(item.Attributes["ny"].Value.Remove(item.Attributes["ny"].Value.Length - 1)));
                                    wr.Write(float.Parse(item.Attributes["nz"].Value.Remove(item.Attributes["nz"].Value.Length - 1)));
                                    wr.Write(int.Parse(item.Attributes["val"].Value));
                                }
                            }
                        }
                        value = Convert.ToBase64String(ms.ToArray());
                    }
                    if (!data.Contains(value))
                        data.Add(value);
                    return new Data(data.IndexOf(value));
                }
                else
                {
                    NamedNode node = new NamedNode();
                    foreach (XmlNode item in xmlNode.ChildNodes)
                    {
                        if (item.NodeType == XmlNodeType.Element)
                        {
                            if (!nodes.Contains(item.Name))
                                nodes.Add(item.Name);
                            node.Nodes.Add(new KeyValuePair<int, ByamlNode>(nodes.IndexOf(item.Name), FromXml(doc, item, nodes, values, data)));
                        }
                    }
                    foreach (XmlAttribute item in xmlNode.Attributes)
                    {
                        if (!nodes.Contains(item.Name))
                            nodes.Add(item.Name);
                        node.Nodes.Add(new KeyValuePair<int, ByamlNode>(nodes.IndexOf(item.Name), FromXml(doc, item, nodes, values, data)));
                    }
                    return node;
                }
            }
            else
            {
                if (xmlNode.Attributes != null && xmlNode.Attributes["type"] != null)
                {
                    if (xmlNode.Attributes["type"].Value == "string")
                    {
                        if (!values.Contains(xmlNode.InnerText))
                            values.Add(xmlNode.InnerText);
                        return new String(values.IndexOf(xmlNode.InnerText));
                    }
                }

                int value_int;
                bool value_bool;

                if (xmlNode.InnerText.EndsWith("f"))
                    return new Single(float.Parse(xmlNode.InnerText.Remove(xmlNode.InnerText.Length - 1)));
                else if (int.TryParse(xmlNode.InnerText, out value_int))
                    return new Int(value_int);
                else if (bool.TryParse(xmlNode.InnerText, out value_bool))
                    return new Boolean(value_bool);
                else 
                    throw new InvalidDataException();
            }
        }
    }


}
