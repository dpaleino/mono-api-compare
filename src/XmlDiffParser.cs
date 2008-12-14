// XmlDiffParser.cs
// 
// Copyright © 2008 David Paleino <d.paleino@gmail.com>
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml;

namespace PkgMono.API {
	public class XmlDiffParser {
		private List<string> missing;
		private List<string> extra;
		private List<string> _new;
		private Hashtable extra_analysis;
		private Hashtable new_analysis;
		
		public XmlDiffParser() {}
		
		public XmlDiffParser(string xml) {
			MakeAnalysis(xml);
		}
		
		public void MakeAnalysis(string xml) {
			missing = new List<string>();
			extra = new List<string>();
			_new = new List<string>();
			extra_analysis = new Hashtable();
			new_analysis = new Hashtable();

//			Console.WriteLine(xml);
//			Console.WriteLine();
			
			ParseClasses(xml);
		}
		
		public void ParseClasses(string xml) {
//			try {
				XmlDocument dom = new XmlDocument();
				dom.LoadXml(xml);
				
				XmlNodeList classes = dom.GetElementsByTagName("class");
				foreach (XmlNode node in classes) {
					string className = "";
					foreach (XmlAttribute attr in node.Attributes) {
						switch (attr.Name) {
							case "name":
								className = attr.InnerText;
								break;
							case "complete_total":
								if (attr.InnerText != "100") {
									if (!missing.Contains(className)) {
										missing.Add(className);
									}
								}
								break;
							case "missing":
								if (!missing.Contains(className)) {
									missing.Add(className);
								}
								break;
							case "presence":
								if (node.HasChildNodes) {
									// it's not totally new
									extra.Add(className);
								} else {
									_new.Add(className);
//									Console.WriteLine("FOO: {0}", className);
								}
								break;
							default:
								break;
						}
					}
					
					if (_new.Contains(className)) {
						LookTotallyNew(className, node);
					}
					else {
						if (missing.Contains(className)) {
							LookFor("missing", className, node.ChildNodes);
						}
						if (extra.Contains(className)) {
							LookFor("extra", className, node.ChildNodes);
						}
					}
				}
//			}
//			catch (Exception ex) {
//				Console.WriteLine(ex.Message);
//			}
		}
		
		/// <summary>
		/// Looks for missing/extra "members" of classes.
		/// </summary>
		/// <param name="what">
		/// A <see cref="System.String"/>, "missing" or "extra".
		/// </param>
		/// <param name="parent">
		/// A <see cref="System.String"/>, the class to analyze.
		/// </param>
		/// <param name="nodes">
		/// A <see cref="XmlNodeList"/>, the children of the class node.
		/// </param>
		/// <remarks>
		/// Doesn't parse (thus not adding to the resulting analysis) totally
		/// "extra" classes, i.e. in the form:
		///   &lt;class name="AssemblyReader" type="class" presence="extra" ok="1" ok_total="1" extra="1" extra_total="1" /&gt;
		/// See LookTotallyNew(string name, XmlNode node).
		/// </remarks>
		public void LookFor(string what, string parent, XmlNodeList nodes) {
			foreach (XmlNode child in nodes) {
				// we don't support these, sorry.
				if (
				    (child.Name == "warning") ||
				    (child.Name == "attribute") ||
				    (child.Name == "parameter")
				   ) {
					continue;
				}
				if (child.HasChildNodes) {
					LookFor(what, parent, child.ChildNodes);
				} else {
					try {
						/*
						 * class
						 *   missing
						 *     <type> (child.Name)
						 *       name
						 *       ...
						 *     ...
						 *   extra
						 *     <type>
						 *       name
						 *       ...
						 *     ...
						 * otherclass
						 *   ...
						 */
						if (this.extra_analysis.Contains(parent)) {
							Hashtable myclass = this.extra_analysis[parent] as Hashtable;
							if (!myclass.Contains(what)) {
								Utils.Debug(1, "Class {0} doesn't have \"{1}\" section of analysis table, adding.",
								            parent, what);
								myclass.Add(what, new Hashtable());
							}
							Hashtable types = myclass[what] as Hashtable;
							if (!types.Contains(child.Name)) {
								types.Add(child.Name, new List<string>());
							}
							
							foreach (DictionaryEntry d in myclass[what] as Hashtable) {
								if (d.Key.ToString() == child.Name) {
									List<string> typef = d.Value as List<string>;
									string type = child.Attributes["name"].InnerText;
									if (!typef.Contains(type)) {
										Utils.Debug("Adding {0} {1}::{2}.", child.Name, parent, type);
										typef.Add(type);
									}
//									Console.Write("{0}: ", d.Key);
//									foreach(string s in typef) {
//										Console.Write("{0} ", s);
//									}
//									Console.WriteLine();
								}
							}
						}
						else {
							Utils.Debug(1, "Class {0} not found in analysis table, adding.", parent);
							Utils.Debug(1, "Class {0} doesn't have \"{1}\" section in analysis table, adding.",
							            parent, what);
							Utils.Debug("Adding {0} {1}::{2}.",
							            child.Name, parent, child.Attributes["name"].InnerText);
							
							// nested Hashtables are fun, really.
							List<string> type = new List<string>();
							Hashtable types = new Hashtable();
							Hashtable table = new Hashtable();
							type.Add(child.Attributes["name"].InnerText);
							types.Add(child.Name, type);
							table.Add(what, types);
							this.extra_analysis.Add(parent, table);
						}
					}
					catch (Exception ex) {
						Console.WriteLine(ex.Message);
					}
				}
			}
		}

		/// <summary>
		/// Parses newly added classes.
		/// </summary>
		/// <param name="name">
		/// A <see cref="System.String"/>, the class to be added.
		/// </param>
		/// <param name="node">
		/// A <see cref="XmlNode"/>, the node corresponding to <paramref name="name"/>
		/// </param>
		public void LookTotallyNew(string name, XmlNode node) {
			if (this.new_analysis.Contains(node.Name)) {
				(this.new_analysis[node.Name] as List<string>).Add(name);
				Utils.Debug("{0} {1} not found in analysis of newly added objects, adding.",
				            node.Name, name);
			}
			else {
				List<string> members = new List<string>();
				members.Add(name);
				this.new_analysis.Add(node.Name, members);
				Utils.Debug(1, "{0} object not found in analysis of newly added objects, adding.",
				            node.Name);
				Utils.Debug("{0} {1} not found in analysis of newly added objects, adding.",
				            node.Name, name);
			}
		}
				
		/// <value>
		/// The format is:
		/// (
		///  "class" => (
		///              "missing => (
		///                           "&lt;type&gt;" => (
		///                                        name,
		///                                        name,
		///                                        ...
		///                                       ),
		///                           ...
		///                          ),
		///              "extra" => (...),
		///            ),
		///  "otherclass" => (...)
		/// )
		/// </value>
		public Hashtable MissingExtraAnalysis {
			get {
				return this.extra_analysis;
			}
		}
		
		/// <value>
		/// The format is:
		/// (
		///  &lt;type&gt; => (
		///             "type1",
		///             "type2",
		///             ...
		///            ),
		///  &lt;othertype&gt; => (...)
		/// )
		/// </value>
		public Hashtable TotallyNewAnalysis {
			get {
				return this.new_analysis;
			}
		}
	}
}
