using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

namespace yamlconv
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("yamlconv");
                Console.WriteLine(" by Chadderz");
                Console.WriteLine("");
                Console.WriteLine("Converts byaml <-> xml");
                Console.WriteLine("");
                Console.WriteLine("Usage: yamlconv file1 [file2 ...]");
            }

            foreach (var arg in args)
            {
                ConvertPath(arg);
            }
        }

        private static void ConvertPath(string path)
        {
            using (var reader = new EndianBinaryReader(new FileStream(path, FileMode.Open)))
            {
                string outpath;
                string magic;
                bool toyaml;

                magic = reader.ReadString(Encoding.ASCII, 2);
                if (magic == "BY")
                {
                    toyaml = true;
                    outpath = Path.ChangeExtension(path, "xml");
                    if (outpath == path)
                        outpath = path + ".xml";
                    reader.Endianness = Endianness.BigEndian;
                }
                else if (magic == "YB")
                {
                    toyaml = true;
                    outpath = Path.ChangeExtension(path, "xml");
                    if (outpath == path)
                        outpath = path + ".xml";
                    reader.Endianness = Endianness.LittleEndian;
                }
                else
                {
                    toyaml = false;
                    outpath = Path.ChangeExtension(path, "byaml");
                    if (outpath == path)
                        outpath = path + ".byaml";
                }

                reader.BaseStream.Seek(0, SeekOrigin.Begin);

                if (toyaml)
                {
                    ConvertFromByaml(reader, outpath);
                }
                else
                {
                    ConvertToByaml(reader, outpath);
                }

                reader.Close();
            }
        }

        private static void ConvertFromByaml(EndianBinaryReader reader, string outpath)
        {
            if (reader.ReadUInt16() != 0x4259)
                throw new InvalidDataException();
            if (reader.ReadUInt16() != 0x0001)
                throw new InvalidDataException();

            uint nodeOffset = reader.ReadUInt32();
            if (nodeOffset > reader.BaseStream.Length)
                throw new InvalidDataException();

            // Number of offset values.
            // Splatoon byamls are missing dataOffset.
            uint offsetCount = nodeOffset == 0x10 ? 3u : 4u;

            uint valuesOffset = reader.ReadUInt32();
            if (valuesOffset > reader.BaseStream.Length)
                throw new InvalidDataException();

            uint dataOffset = offsetCount > 3 ? reader.ReadUInt32() : 0;
            if (dataOffset > reader.BaseStream.Length)
                throw new InvalidDataException();

            uint treeOffset = reader.ReadUInt32();
            if (treeOffset > reader.BaseStream.Length)
                throw new InvalidDataException();


            List<string> nodes = new List<string>();
            List<string> values = new List<string>();
            List<byte[]> data = new List<byte[]>();

            if (nodeOffset != 0)
            {
                reader.BaseStream.Seek(nodeOffset, SeekOrigin.Begin);
                nodes.AddRange(new ByamlNode.StringList(reader).Strings);
            }
            if (valuesOffset != 0)
            {
                reader.BaseStream.Seek(valuesOffset, SeekOrigin.Begin);
                values.AddRange(new ByamlNode.StringList(reader).Strings);
            }
            if (dataOffset != 0)
            {
                reader.BaseStream.Seek(dataOffset, SeekOrigin.Begin);
                data.AddRange(new ByamlNode.BinaryDataList(reader).DataList);
            }

            ByamlNode tree;
            ByamlNodeType rootType;
            reader.BaseStream.Seek(treeOffset, SeekOrigin.Begin);
            rootType = (ByamlNodeType)reader.ReadByte();
            reader.BaseStream.Seek(-1, SeekOrigin.Current);
            if (rootType == ByamlNodeType.UnamedNode)
                tree = new ByamlNode.UnamedNode(reader);
            else
                tree = new ByamlNode.NamedNode(reader);

            XmlDocument yaml = new XmlDocument();
            yaml.AppendChild(yaml.CreateXmlDeclaration("1.0", "UTF-8", null));
            XmlElement root = yaml.CreateElement("yaml");
            XmlAttribute xmlnsAttribute = yaml.CreateAttribute("xmlns:yamlconv");
            xmlnsAttribute.InnerText = "yamlconv";
            root.Attributes.Append(xmlnsAttribute);
            XmlAttribute endianAttribute = yaml.CreateAttribute("endianness", "yamlconv");
            endianAttribute.InnerText = reader.Endianness == Endianness.BigEndian ? "big" : "little";
            root.Attributes.Append(endianAttribute);
            XmlAttribute offsetCountAttribute = yaml.CreateAttribute("offsetCount", "yamlconv");
            offsetCountAttribute.InnerText = offsetCount.ToString();
            root.Attributes.Append(offsetCountAttribute);
            yaml.AppendChild(root);

            tree.ToXml(yaml, root, nodes, values, data);

            using (StreamWriter writer = new StreamWriter(new FileStream(outpath, FileMode.Create), Encoding.UTF8))
            {
                yaml.Save(writer);
            }
        }

        private static void ConvertToByaml(EndianBinaryReader reader, string outpath)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(reader.BaseStream);

            if (doc.LastChild.Name != "yaml")
                throw new InvalidDataException();

            uint offsetCount = 4;
            if (doc.LastChild.Attributes["yamlconv:offsetCount"] != null)
                offsetCount = uint.Parse(doc.LastChild.Attributes["yamlconv:offsetCount"].Value);

            Endianness endianness = Endianness.BigEndian;
            if (doc.LastChild.Attributes["yamlconv:endianness"] != null)
                endianness = doc.LastChild.Attributes["yamlconv:endianness"].Value == "little" ? Endianness.LittleEndian : Endianness.BigEndian;

            List<string> nodes = new List<string>();
            List<string> values = new List<string>();
            List<string> data = new List<string>();

            ByamlNode tree = ByamlNode.FromXml(doc, doc.LastChild, nodes, values, data);

            List<ByamlNode> flat = new List<ByamlNode>();
            Stack<ByamlNode> process = new Stack<ByamlNode>();

            List<string> sorted_nodes = new List<string>();
            sorted_nodes.AddRange(nodes);
            sorted_nodes.Sort(StringComparer.Ordinal);
            List<string> sorted_values = new List<string>();
            sorted_values.AddRange(values);
            sorted_values.Sort(StringComparer.Ordinal);

            process.Push(tree);
            while (process.Count > 0)
            {
                ByamlNode current = process.Pop();
                flat.Add(current);

                if (current.GetType() == typeof(ByamlNode.NamedNode))
                {
                    ByamlNode.NamedNode cur = current as ByamlNode.NamedNode;
                    SortedDictionary<int, ByamlNode> dict = new SortedDictionary<int, ByamlNode>();
                    Stack<ByamlNode> reverse = new Stack<ByamlNode>();
                    foreach (var item in cur.Nodes)
                    {
                        dict.Add(sorted_nodes.IndexOf(nodes[item.Key]), item.Value);
                        reverse.Push(item.Value);
                    }
                    while (reverse.Count > 0)
                        process.Push(reverse.Pop());
                    cur.Nodes.Clear();
                    foreach (var item in dict)
                        cur.Nodes.Add(item);
                }
                else if (current.GetType() == typeof(ByamlNode.UnamedNode))
                {
                    Stack<ByamlNode> reverse = new Stack<ByamlNode>();
                    foreach (var item in (current as ByamlNode.UnamedNode).Nodes)
                    {
                        reverse.Push(item);
                    }
                    while (reverse.Count > 0)
                        process.Push(reverse.Pop());
                }
                else if (current.GetType() == typeof(ByamlNode.String))
                {
                    ByamlNode.String cur = current as ByamlNode.String;

                    cur.Value = sorted_values.IndexOf(values[cur.Value]);
                }
            }

            using (EndianBinaryWriter writer = new EndianBinaryWriter(new FileStream(outpath, FileMode.Create)))
            {
                writer.Endianness = endianness;
                uint[] off = new uint[offsetCount];

                for (int i = 0; i < 2; i++)
                {
                    writer.BaseStream.Position = 0;

                    writer.Write((UInt16)0x4259);
                    writer.Write((UInt16)0x0001);
                    writer.Write(off, 0, (int)offsetCount);

                    if (sorted_nodes.Count > 0)
                    {
                        off[0] = (uint)writer.BaseStream.Position;

                        int len = 8 + 4 * sorted_nodes.Count;
                        writer.Write(sorted_nodes.Count | ((int)ByamlNodeType.StringList << 24));
                        foreach (var item in sorted_nodes)
                        {
                            writer.Write(len);
                            len += item.Length + 1;
                        }
                        writer.Write(len);
                        foreach (var item in sorted_nodes)
                            writer.Write(item, Encoding.ASCII, true);
                        writer.WritePadding(4, 0);
                    }
                    else
                        off[0] = 0;

                    if (sorted_values.Count > 0)
                    {
                        off[1] = (uint)writer.BaseStream.Position;

                        int len = 8 + 4 * sorted_values.Count;
                        writer.Write(sorted_values.Count | ((int)ByamlNodeType.StringList << 24));
                        foreach (var item in sorted_values)
                        {
                            writer.Write(len);
                            len += item.Length + 1;
                        }
                        writer.Write(len);
                        foreach (var item in sorted_values)
                            writer.Write(item, Encoding.ASCII, true);
                        writer.WritePadding(4, 0);
                    }
                    else
                        off[1] = 0;

                    if (offsetCount > 3 && data.Count > 0)
                    {
                        off[2] = (uint)writer.BaseStream.Position;

                        int len = 8 + 4 * data.Count;
                        writer.Write(data.Count | ((int)ByamlNodeType.BinaryDataList << 24));
                        foreach (var item in data)
                        {
                            byte[] val = Convert.FromBase64String(item);
                            writer.Write(len);
                            len += val.Length;
                        }
                        writer.Write(len);
                        foreach (var item in data)
                        {
                            byte[] val = Convert.FromBase64String(item);
                            writer.Write(val, 0, val.Length);
                        }
                        writer.WritePadding(4, 0);
                    }
                    else
                        off[2] = 0;

                    off[off.Length - 1] = (uint)writer.BaseStream.Position;

                    foreach (var current in flat)
                    {
                        current.Address = writer.BaseStream.Position;
                        if (current.GetType() == typeof(ByamlNode.NamedNode))
                        {
                            ByamlNode.NamedNode cur = current as ByamlNode.NamedNode;
                            writer.Write(cur.Nodes.Count | ((int)cur.Type << 24));
                            foreach (var item in cur.Nodes)
                            {
                                writer.Write(((int)item.Value.Type) | (item.Key << 8));
                                switch (item.Value.Type)
                                {
                                    case ByamlNodeType.String:
                                        writer.Write((item.Value as ByamlNode.String).Value);
                                        break;
                                    case ByamlNodeType.Data:
                                        writer.Write((item.Value as ByamlNode.Data).Value);
                                        break;
                                    case ByamlNodeType.Boolean:
                                        writer.Write((item.Value as ByamlNode.Boolean).Value ? 1 : 0);
                                        break;
                                    case ByamlNodeType.Int:
                                        writer.Write((item.Value as ByamlNode.Int).Value);
                                        break;
                                    case ByamlNodeType.Single:
                                        writer.Write((item.Value as ByamlNode.Single).Value);
                                        break;
                                    case ByamlNodeType.UnamedNode:
                                    case ByamlNodeType.NamedNode:
                                        writer.Write((int)item.Value.Address);
                                        break;
                                    default:
                                        throw new NotImplementedException();
                                }
                            }
                        }
                        else if (current.GetType() == typeof(ByamlNode.UnamedNode))
                        {
                            ByamlNode.UnamedNode cur = current as ByamlNode.UnamedNode;
                            writer.Write(cur.Nodes.Count | ((int)cur.Type << 24));
                            foreach (var item in cur.Nodes)
                                writer.Write((byte)item.Type);
                            writer.WritePadding(4, 0);
                            foreach (var item in cur.Nodes)
                            {
                                switch (item.Type)
                                {
                                    case ByamlNodeType.String:
                                        writer.Write((item as ByamlNode.String).Value);
                                        break;
                                    case ByamlNodeType.Data:
                                        writer.Write((item as ByamlNode.Data).Value);
                                        break;
                                    case ByamlNodeType.Boolean:
                                        writer.Write((item as ByamlNode.Boolean).Value ? 1 : 0);
                                        break;
                                    case ByamlNodeType.Int:
                                        writer.Write((item as ByamlNode.Int).Value);
                                        break;
                                    case ByamlNodeType.Single:
                                        writer.Write((item as ByamlNode.Single).Value);
                                        break;
                                    case ByamlNodeType.UnamedNode:
                                    case ByamlNodeType.NamedNode:
                                        writer.Write((int)item.Address);
                                        break;
                                    default:
                                        throw new NotImplementedException();
                                }
                            }
                        }

                    }
                }

                writer.Close();
            }
        }
    }
}
