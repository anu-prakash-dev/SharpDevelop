﻿/*
 * Created by SharpDevelop.
 * User: Peter Forstmeier
 * Date: 03.04.2013
 * Time: 20:10
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using ICSharpCode.Reporting.Exporter;
using ICSharpCode.Reporting.Exporter.Visitors;
using ICSharpCode.Reporting.Interfaces.Export;

namespace ICSharpCode.Reporting.Test
{
	/// <summary>
	/// Description of TestHelper.
	/// </summary>
	public static class TestHelper
	{
		const string nameSpace = "ICSharpCode.Reporting.Test.src.TestReports.";
		const string plainReportName = "PlainModel.srd";
		const string withTwoItems = "ReportWithTwoItems.srd";
		const string fromList = "FromList.srd";
		const string groupedList = "GroupedList.srd";
		const string globalsTestReport = "TestForGlobals.srd";
		
		public static string PlainReportFileName{
			get{return nameSpace + plainReportName;}
		}
		
		
		public static string RepWithTwoItems {
			get {return nameSpace + withTwoItems;}
		}
		
		
		public static string ReportFromList {
			get {return nameSpace + fromList;}
		}
		
		public static string GroupedList {
			get {return nameSpace + groupedList;}
		}
		
		
		public static string TestForGlobals {
			get {return nameSpace + globalsTestReport;}
		}
		
		
		public static void ShowDebug(IExportContainer exportContainer)
		{
			var visitor = new DebugVisitor();
			foreach (var item in exportContainer.ExportedItems) {
				var container = item as IExportContainer;
				var acceptor = item as IAcceptor;
				if (container != null) {
					if (acceptor != null) {
						Console.WriteLine("----");
						acceptor.Accept(visitor);
					}
					ShowDebug(container);
				} else {
					if (acceptor != null) {
						acceptor.Accept(visitor);
						
					}
				}
			}
		}
	}
}
