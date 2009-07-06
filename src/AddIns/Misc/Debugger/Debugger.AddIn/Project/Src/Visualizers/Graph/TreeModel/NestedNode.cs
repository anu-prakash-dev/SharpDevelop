﻿// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Martin Koníček" email="martin.konicek@gmail.com"/>
//     <version>$Revision$</version>
// </file>
using System;
using System.Collections.Generic;

namespace Debugger.AddIn.Visualizers.Graph
{
	/// <summary>
	/// Description of NestedNode.
	/// </summary>
	public class NestedNode : AbstractNode
	{
		private NestedNodeType nodeType;
		public NestedNodeType NodeType
		{
			get { return nodeType; }
		}
		
		public NestedNode(NestedNodeType nodeType)
		{
			this.nodeType = nodeType;
		}
	}
}
