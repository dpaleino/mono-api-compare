// MyClass.cs
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

namespace testlibrary1
{
	public class MyClass
	{
		public string PublicString;
		private string PrivateString;
		
		public MyClass() {}
		public MyClass(string foo, int bar) {}
		public MyClass(string foo, string bar, string baz) {}
		
		public void TestMethod() {}
		public void TestMethod(string foo) {}
		
		public void OtherMethod() {}
	}
}
