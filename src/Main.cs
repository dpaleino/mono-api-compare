// Main.cs
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
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using NDesk.Options;

namespace PkgMono.API {
	public class Comparer {
		static string diff_xml = "";
		
		public static void Main(string[] args) {
			OptionSet opts = new OptionSet();
			opts.Add("v|verbose", "Be verbose.", delegate (string v) { if (v != null) ++Defines.Verbosity; });
			opts.Add("h|?|help", "Show this help message.", delegate (string v) { Defines.ShowHelp = v != null; });
			opts.Add("g|growth|api-growth", "Show only API growths.", delegate (string v) { Defines.OnlyGrowth = v != null; });
			opts.Add("b|breakage|api-breakage", "Show only API breakages.", delegate (string v) { Defines.OnlyBreakage = v != null; });
			
			List<string> dlls = new List<string>();
			try {
				dlls = opts.Parse(args);
				if (dlls.Count != 2) {
					ShowHelp(opts);
					return;
				}
			}
			catch (OptionException ex) {
				Console.WriteLine("mono-api-comparer: {0}", ex.Message);
				Console.WriteLine("Try `mono-api-comparer --help' for more information.");
				return;
			}
			catch (Exception ex) {
				Console.WriteLine(ex.Message);
			}
			
			if (Defines.ShowHelp) {
				ShowHelp(opts);
				return;
			}
			
			try {
				diff_xml = GenerateXMLDiff(dlls);
			}
			catch (Exception ex) {
				Console.WriteLine(ex.Message);
			}
			
			XmlDiffParser parser = new XmlDiffParser(diff_xml);
			Hashtable extra_analysis = parser.MissingExtraAnalysis;
			Hashtable new_analysis = parser.TotallyNewAnalysis;
			
			ReflectAssembly(dlls, extra_analysis, new_analysis);
			
//			foreach (DictionaryEntry d in parser.Results) {
//				Console.Write(d.Key + ": ");
//				foreach (DictionaryEntry o in (Hashtable)d.Value) {
//					Console.WriteLine("{0} - {1}",
//					                  o.Key,
//					                  o.Value);
//				}
//			}
		}
		
		public static void ShowHelp(OptionSet opts) {
			Console.WriteLine("mono-api-comparer {0}", Defines.Version);
			Console.WriteLine();
			Console.WriteLine("Usage: mono-api-comparer [OPTIONS]+ old-assembly.dll new-assembly.dll");
			Console.WriteLine("Compare APIs between two assemblies");
			Console.WriteLine();
			Console.WriteLine("Options:");
			opts.WriteOptionDescriptions(Console.Out);
		}
		
		public static string GenerateXMLDiff(IList<string> dlls) {
			string old_lib = dlls[0];
			string new_lib = dlls[1];
			string old_xml = old_lib + ".xml";
			string new_xml = new_lib + ".xml";
			string diff_xml = "api_diff.xml";
			
			string api_info = "mono-api-info2";
			string api_diff = "mono-api-diff";
			
			ProcessStartInfo psi = new ProcessStartInfo();
			psi.FileName = "/bin/sh";
			psi.Arguments = String.Format(" -c '{0} {1} 2>/dev/null > {2}'",
			                              api_info,
			                              old_lib,
			                              old_xml);
			Process proc = Process.Start(psi);
			proc.WaitForExit();
			proc.Dispose();
			
			psi.Arguments = String.Format(" -c '{0} {1} 2>/dev/null > {2}'",
			                              api_info,
			                              new_lib,
			                              new_xml);
			proc = Process.Start(psi);
			proc.WaitForExit();
			proc.Dispose();
			
			psi.Arguments = String.Format(" -c '{0} {1} {2} 2>/dev/null > {3}'",
			                              api_diff,
			                              old_xml,
			                              new_xml,
			                              diff_xml);
			proc = Process.Start(psi);
			proc.WaitForExit();
			proc.Dispose();
			
			StreamReader sr = new StreamReader(diff_xml);
			string xml = sr.ReadToEnd();
			sr.Close();
			
			// remove all the files
			File.Delete(old_xml);
			File.Delete(new_xml);
			File.Delete(diff_xml);
			
			return xml;
		}
		
		public static void ReflectAssembly(IList<string> dlls, Hashtable extra_analysis, Hashtable new_analysis) {
			Assembly odll = Assembly.LoadFrom(dlls[0]);
			Assembly ndll = Assembly.LoadFrom(dlls[1]);
			Type[] otypes = odll.GetExportedTypes();
			Type[] ntypes = ndll.GetExportedTypes();
			
			Console.WriteLine("Total to analyze: "+extra_analysis.Count);
			foreach (Type t in ntypes) {
				if (extra_analysis.Contains(t.Name)) {
					Hashtable table = extra_analysis[t.Name] as Hashtable;
					Hashtable missing = new Hashtable();
					Hashtable extra = new Hashtable();
					int mcount = 0;
					int ecount = 0;
					
					try {
						missing = table["missing"] as Hashtable;
						foreach (DictionaryEntry d in missing) {
							mcount += (d.Value as List<string>).Count;
						}
						extra = table["extra"] as Hashtable;
						foreach (DictionaryEntry d in extra) {
							ecount += (d.Value as List<string>).Count;
						}
					}
					catch {}
					
					Utils.Debug("Analysing {0} from the new assembly ({1} missing, {2} extra)...",
					            t.Name, mcount, ecount);
					
					if (mcount > 0) {
						foreach (DictionaryEntry d in missing) {
							// d.Key is "property", "method", ...
							string type = d.Key.ToString();
							List<string> names = d.Value as List<string>;
							Type[] parents = GetParentTypes(t);
//							foreach (Type p in parents) {
//								Console.WriteLine("P:"+p.Name);
//							}
							
							switch (type) {
								case "constructor":
									// ctors aren't inherited.
									foreach (string s in names) {
										string name = s.Substring(0, s.IndexOf('('));
										string sign = s.Substring(s.IndexOf('('));
										Console.WriteLine("Can't find {0} {1} with signature {2} in {3}::{4}",
										                  type, name, sign, dlls[1], t.Name);
									}
									break;
								case "property":
									List<string> properties = new List<string>();
									foreach (Type pt in parents) {
										foreach (PropertyInfo p in pt.GetProperties()) {
											properties.Add(p.Name);
										}
									}
									foreach (string s in names) {
										if (!properties.Contains(s)) {
											Console.WriteLine("Can't find {0} {1} in {2}::{3}.",
											                  type, s, dlls[1], t.Name);
										}
									}
									break;
								case "method":
									Hashtable methods = new Hashtable();
									foreach (Type pt in parents) {
										foreach (MethodInfo m in pt.GetMethods()) {
//											Console.WriteLine("M:"+m.Name);
											string sign = m.ToString().Substring(m.ToString().IndexOf('('));
											if (!methods.Contains(m.Name)) {
												List<string> signatures = new List<string>();
												signatures.Add(sign);
												methods.Add(m.Name, signatures);
											} else {
												List<string> signatures = (methods[m.Name] as List<string>);
												if (!signatures.Contains(sign)) {
													signatures.Add(sign);
												}
											}
										}
									}
									foreach (string method in names) {
										bool found = false;
										string name = method.Substring(0, method.IndexOf('('));
										string sign = method.Substring(method.IndexOf('('));
										foreach (DictionaryEntry e in methods) {
											if (e.Key.ToString() == name) {
//												Console.WriteLine("K:"+e.Key);
												foreach (string s in e.Value as List<string>) {
//													Console.WriteLine(s);
													if (t.GetMethod(e.Key.ToString(), GetTypesFromSignature(s)) != null) {
														Utils.Debug("Method {0} {1} found in base class, false positive.",
														            e.Key, s);
														found = true;
													}
//													else {
//														Console.WriteLine("Can't find method {0} {1} in {2}::{3}.",
//														                  e.Key, s, dlls[1], t.Name);
//													}
												}
												break;
											}
										}
										if (!found) {
											Console.WriteLine("Can't find method {0} {1} in {2}::{3}.",
											                  name, sign, dlls[1], t.Name);
										}
									}
									break;
								default:
									foreach (string s in names) {
										Console.WriteLine("NOT_IMPL: {0} {1} missing.",
										                  type, s);
									}
									break;
							}
							
//							Console.Write("{0}:", type);
//							foreach (string name in names) {
//								Console.Write(" {0}", name);
//							}
//							Console.WriteLine();
						}
					}
					if (ecount > 0) {
					}
//					Utils.Debug("Analyzing class {0} in the new assembly...", t.Name);
//					foreach (DictionaryEntry d in (Hashtable)(analysis[t.Name])) {
//						bool itemFound = false;
//						string tmp = d.Key.ToString();
//						string item = tmp;
//						string signature = "";
//						try {
//							item = tmp.Substring(0, tmp.IndexOf('('));
//							signature = tmp.Substring(tmp.IndexOf('('));
//						}
//						catch (Exception ex) {
//						}
//						
//						if (item == ".ctor") {
//							// constructors aren't inherited, skip them
//							Console.WriteLine("Can't find constructor with signature {0} in {1}::{2}.",
//							                  signature,
//							                  dlls[1],
//							                  t.Name);
//							continue;
//						}
//						else {
//							foreach (MemberInfo m in t.GetMembers()) {
//								if (m.Name == item) {
//									// we have the same method in the base class,
//									// check its signature.
//									itemFound = true;
//									Type[] types = GetTypesFromSignature(signature);
//									Console.WriteLine(t.GetMethod(item, types));
//									if (t.GetMethod(item, types) != null) {
//										Utils.Debug("Method {0}, with signature {1} found in base class, false positive.",
//										      item,
//										      signature);
//									} else {
//										Console.WriteLine("Can't find method {0} with signature {1} in {2}::{3}.",
//										                  item,
//										                  signature,
//										                  dlls[1],
//										                  t.Name);
//									}
//								}
//							}
//						}
//						if (!itemFound) {
//							Console.WriteLine("Can't find item {0} with signature {1} in {2}::{3}.",
//							                  item,
//							                  signature,
//							                  dlls[1],
//							                  t.Name);
//						}
//					}
				}
				else {
					bool found = false;
					foreach (DictionaryEntry d in new_analysis) {
//						string type = d.Key.ToString();
						List<string> names = (List<string>)d.Value;
						if (names.Contains(t.Name)) {
							found = true;
							Hashtable _new = new Hashtable();
							// Implement this.
						}
					}
					if (!found) {
						/*
						 * It's not in the diff, check whether it's in the "old"
						 * types. If not, this is probably a mono-api-diff bug.
						 */
						bool old_found = false;
						foreach (Type ot in otypes) {
							if (t.Name == ot.Name) {
								Utils.Debug("Class {0} has not changed.", t.Name);
								old_found = true;
							}
						}
						if (!old_found) {
							/*
							 * This seems too much verbose, but it's a bug probably.
							 */
//							Console.WriteLine("\n----------");
//							Console.WriteLine("Cannot find {0} in analysis tables, nor in the old assembly.",
//							                  t.Name);
//							Console.WriteLine("This is probably a bug in mono-api-diff: a new class has not been output in the diff XML.");
//							Console.WriteLine("----------\n");
						}						
					}
				}
			}
//			foreach (Type t in ntypes) {
//				if (analysis.Contains(t.Name)) {
//					
//				}
//				else {
//					Console.WriteLine("Didn't find {0} in analysis table!",
//					                  t.Name);
//				}
//			}
		}
		
		public static Type[] GetTypesFromSignature(string signature) {
			List<Type> types = new List<Type>();
			string pattern = "([a-zA-Z0-9.]+)";

			MatchCollection matches = Regex.Matches(signature, pattern);
			if (matches.Count > 0) {
				foreach (Match m in matches) {
					types.Add(Type.GetType(m.ToString()));
				}
			}
			return types.ToArray();		
		}
		
		public static Type[] GetParentTypes(Type type) {
			List<Type> types = new List<Type>();
			if (type.BaseType == null) {
				// we reached Object, it seems.
				types.Add(type);
				return types.ToArray();
			}
			else {
				types.Add(type);
				foreach (Type t in GetParentTypes(type.BaseType)) {
					types.Add(t);
				}
			}
			return types.ToArray();
		}
	}
}