using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Xml.Serialization;
using ICSharpCode.Core;

namespace ICSharpCode.Core.Presentation
{	
	/// <summary>
	/// Global registry to store and access commands, command bindings and input bindings
	/// </summary>
	public static class CommandManager
	{
		/// <summary>
		/// Default application context.
		/// 
		/// This should be set to the root UI element
		/// </summary>
		public static string DefaultContextName {
			get; set;
		}
		
		// Binding infos
		private static HashSet<IBindingInfo> commandBindings = new HashSet<IBindingInfo>();
		private static HashSet<IBindingInfo> inputBidnings = new HashSet<IBindingInfo>();
		
		// Commands
		private static Dictionary<string, RoutedUICommand> routedCommands = new Dictionary<string, RoutedUICommand>();
		internal static Dictionary<string, System.Windows.Input.ICommand> commands = new Dictionary<string, System.Windows.Input.ICommand>();
		
		// Binding update handlers
		private static Dictionary<IBindingInfo, HashSet<BindingsUpdatedHandler>> inputBindingUpdatedHandlers = new Dictionary<IBindingInfo, HashSet<BindingsUpdatedHandler>>();
		private static Dictionary<IBindingInfo, HashSet<BindingsUpdatedHandler>> commandBindingUpdatedHandlers = new Dictionary<IBindingInfo, HashSet<BindingsUpdatedHandler>>();
		
		// Named instances and types
		private static Dictionary<string, HashSet<UIElement>> namedUIInstances = new Dictionary<string, HashSet<UIElement>>();
		private static Dictionary<string, HashSet<Type>> namedUITypes = new Dictionary<string, HashSet<Type>>();
		
		// Reverse named instances and types (used to search for instance name by type)
		private static Dictionary<UIElement, HashSet<string>> reverseNamedUIInstances = new Dictionary<UIElement, HashSet<string>>();
		private static Dictionary<Type, HashSet<string>> reverseNamedUITypes = new Dictionary<Type, HashSet<string>>();
		
		// Categories
		public static List<InputBindingCategory> InputBindingCategories = new List<InputBindingCategory>();
		
		/// <summary>
		/// Register UI element instance accessible by unique name
		/// </summary>
		/// <param name="instanceName">Instance name</param>
		/// <param name="element">Instance</param>
		public static void RegisterNamedUIElement(string instanceName, UIElement element)
		{	
			if(!namedUIInstances.ContainsKey(instanceName)){
				namedUIInstances.Add(instanceName, new HashSet<UIElement>());
			}
			
			if(!reverseNamedUIInstances.ContainsKey(element)) {
				reverseNamedUIInstances.Add(element, new HashSet<string>());
			}
			
			if(namedUIInstances[instanceName].Add(element)) {
				reverseNamedUIInstances[element].Add(instanceName);
				
				// If there are some bindings and update handlers already registered, 
				// but owner is not loaded then invoke those bindings
				
				InvokeCommandBindingUpdateHandlers(
					BindingInfoMatchType.SuperSet, 
					new BindingInfoTemplate { OwnerInstanceName = instanceName },
					new BindingInfoTemplate { OwnerInstances = new[] { element }});
						
				InvokeInputBindingUpdateHandlers(
					BindingInfoMatchType.SuperSet, 
					new BindingInfoTemplate { OwnerInstanceName = instanceName },
					new BindingInfoTemplate { OwnerInstances = new[] { element }});
			}
		}
		
		public static void UnregisterNamedUIElement(string instanceName, UIElement instance)
		{	
			if(namedUIInstances.ContainsKey(instanceName)){
				if(namedUIInstances[instanceName].Remove(instance)) {
					reverseNamedUIInstances[instance].Remove(instanceName);
				}
				
				InvokeCommandBindingUpdateHandlers(
					BindingInfoMatchType.SuperSet, 
					new BindingInfoTemplate { OwnerInstanceName = instanceName },
					new BindingInfoTemplate { OwnerInstances = new [] { instance }});
						
				InvokeInputBindingUpdateHandlers(
					BindingInfoMatchType.SuperSet, 
					new BindingInfoTemplate { OwnerInstanceName = instanceName },
					new BindingInfoTemplate { OwnerInstances = new [] { instance }});
			}
		}
		
		/// <summary>
		/// Get instance by unique instance name
		/// </summary>
		/// <param name="instanceName">Instance name</param>
		/// <returns></returns>
		public static ICollection<UIElement> GetNamedUIElementCollection(string instanceName)
		{
			HashSet<UIElement> instances;
			namedUIInstances.TryGetValue(instanceName, out instances);
			
			return instances ?? new HashSet<UIElement>();
		}
		
		public static ICollection<string> GetUIElementNameCollection(UIElement instance)
		{
			HashSet<string> names;
			reverseNamedUIInstances.TryGetValue(instance, out names);
			
			return names ?? new HashSet<string>();
		}
		
		/// <summary>
		/// Register UI type which can be accessible by name
		/// </summary>
		/// <param name="typeName">Type name</param>
		/// <param name="type">Type</param>
		public static void RegisterNamedUIType(string typeName, Type type)
		{
			if(type.IsSubclassOf(typeof(UIElement))) {
				throw new ArgumentException("Type is not subclass of UIElement");
			}
			
			if(!namedUITypes.ContainsKey(typeName)){
				namedUITypes.Add(typeName, new HashSet<Type>());
			}
			
			if(!reverseNamedUITypes.ContainsKey(type)) {
				reverseNamedUITypes.Add(type, new HashSet<string>());
			}
			
			if(namedUITypes[typeName].Add(type)) {
				reverseNamedUITypes[type].Add(typeName);
				
				// If any update handlers where assigned to the type and type was not 
				// loaded yet then invoke update handlers
				InvokeCommandBindingUpdateHandlers(
					BindingInfoMatchType.SuperSet, 
					new BindingInfoTemplate { OwnerTypeName = typeName },
					new BindingInfoTemplate { OwnerTypes = new[] { type }});
						
				InvokeInputBindingUpdateHandlers(
					BindingInfoMatchType.SuperSet, 
					new BindingInfoTemplate { OwnerTypeName = typeName },
					new BindingInfoTemplate { OwnerTypes = new[] { type }});
			}
		}
		
		public static void UnregisterNamedUIType(string typeName, Type type)
		{	
			if(namedUITypes.ContainsKey(typeName)){
				if(namedUITypes[typeName].Remove(type)) {
					reverseNamedUITypes[type].Remove(typeName);
				
					InvokeCommandBindingUpdateHandlers(
						BindingInfoMatchType.SuperSet, 
						new BindingInfoTemplate { OwnerTypeName = typeName },
						new BindingInfoTemplate { OwnerTypes = new[] { type }});
							
					InvokeInputBindingUpdateHandlers(
						BindingInfoMatchType.SuperSet, 
						new BindingInfoTemplate { OwnerTypeName = typeName },
						new BindingInfoTemplate { OwnerTypes = new[] { type }});
				}
			}
		}
		
		/// <summary>
		/// Get type by uniqe type name
		/// </summary>
		/// <param name="typeName">Type name</param>
		/// <returns>Type</returns>
		public static ICollection<Type> GetNamedUITypeCollection(string typeName)
		{
			HashSet<Type> instance;
			namedUITypes.TryGetValue(typeName, out instance);
			
			return instance ?? new HashSet<Type>();
		}
		
		public static ICollection<string> GetUITypeNameCollection(Type type)
		{
			HashSet<string> typeNames;
			reverseNamedUITypes.TryGetValue(type, out typeNames);
			
			return typeNames ?? new HashSet<string>();
		}

		/// <summary>
		/// Register new routed command in the global registry
		/// 
		/// Routed command name should be unique in SharpDevelop scope. 
		/// Use "." to organize commands into groups
		/// </summary>
		/// <param name="routedCommandName">Routed command name</param>
		/// <param name="text">Short text describing command functionality</param>
		public static RoutedUICommand RegisterRoutedUICommand(string routedCommandName, string text) {
			if(routedCommands.ContainsKey(routedCommandName)) {
				throw new IndexOutOfRangeException("Routed UI command with name " + routedCommandName + " is already registered");
			}
			
			var routedCommand = new RoutedUICommand(text, routedCommandName, typeof(CommandManager));
			
			routedCommands.Add(routedCommandName, routedCommand);
			
			InvokeCommandBindingUpdateHandlers(
				BindingInfoMatchType.SuperSet, 
				new BindingInfoTemplate { RoutedCommand = routedCommand },
				new BindingInfoTemplate { RoutedCommandName = routedCommandName });
					
			InvokeInputBindingUpdateHandlers(
				BindingInfoMatchType.SuperSet, 
				new BindingInfoTemplate { RoutedCommand = routedCommand },
				new BindingInfoTemplate { RoutedCommandName = routedCommandName });
			
			return routedCommand;
		}

		/// <summary>
		/// Register existing routed command in the global registry
		/// 
		/// Routed command then can be accessed 
		/// Routed command name should be uniq in SharpDevelop scope. 
		/// Use "." to organize commands into groups
		/// </summary>
		/// <param name="routedCommandName">Existing routed command</param>
		public static void RegisterRoutedUICommand(RoutedUICommand existingRoutedUICommand) {
			string routedCommandName = existingRoutedUICommand.OwnerType.Name + "." + existingRoutedUICommand.Name;
			
			if(routedCommands.ContainsKey(routedCommandName)) {
				throw new IndexOutOfRangeException("Routed UI command with name " + routedCommandName + " is already registered");
			}
			
			routedCommands.Add(routedCommandName, existingRoutedUICommand);
			
			InvokeCommandBindingUpdateHandlers(
				BindingInfoMatchType.SuperSet, 
				new BindingInfoTemplate { RoutedCommand = existingRoutedUICommand },
				new BindingInfoTemplate { RoutedCommandName = routedCommandName });
					
			InvokeInputBindingUpdateHandlers(
				BindingInfoMatchType.SuperSet, 
				new BindingInfoTemplate { RoutedCommand = existingRoutedUICommand },
				new BindingInfoTemplate { RoutedCommandName = routedCommandName });
		}
	
		/// <summary>
		/// Remove routed command from global registry
		/// </summary>
		/// <param name="routedCommandName">Routed command name</param>
		public static void UnregisterRoutedUICommand(string routedCommandName) {
			if(routedCommands.ContainsKey(routedCommandName)) {
				var routedCommand = routedCommands[routedCommandName];
				routedCommands.Remove(routedCommandName);
			
				InvokeCommandBindingUpdateHandlers(
					BindingInfoMatchType.SuperSet, 
					new BindingInfoTemplate { RoutedCommand = routedCommand },
					new BindingInfoTemplate { RoutedCommandName = routedCommandName });
						
				InvokeInputBindingUpdateHandlers(
					BindingInfoMatchType.SuperSet, 
					new BindingInfoTemplate { RoutedCommand = routedCommand },
					new BindingInfoTemplate { RoutedCommandName = routedCommandName });
			}
		}
		
		/// <summary>
		/// Get reference to routed UI command by name
		/// </summary>
		/// <param name="routedCommandName">Routed command name</param>
		/// <returns>Routed command instance</returns>
		public static RoutedUICommand GetRoutedUICommand(string routedCommandName) {
			if(routedCommands != null && routedCommands.ContainsKey(routedCommandName)) {
				return routedCommands[routedCommandName];
			}
			
			return null;
		}

		/// <summary>
		/// Register input binding by specifying this binding parameters
		/// </summary>
		/// <param name="inputBindingInfo">Input binding parameters</param>
		public static void RegisterInputBinding(InputBindingInfo inputBindingInfo)
		{
			if(inputBindingInfo.OwnerInstanceName == null && inputBindingInfo.OwnerTypeName == null) {
				throw new ArgumentException("Binding owner must be specified");
			}
			
			if(inputBindingInfo.RoutedCommandName == null) {
				throw new ArgumentException("Routed command name must be specified");
			}
			
			var similarBindingTemplate = new  BindingInfoTemplate();
			similarBindingTemplate.OwnerTypeName = inputBindingInfo.OwnerTypeName;
			similarBindingTemplate.OwnerInstanceName = inputBindingInfo.OwnerInstanceName;
			similarBindingTemplate.RoutedCommandName = inputBindingInfo.RoutedCommandName;
			var similarInputBinding = FindInputBindingInfos(BindingInfoMatchType.SuperSet, similarBindingTemplate).FirstOrDefault();
			
			if(similarInputBinding != null) {
				foreach(InputGesture gesture in inputBindingInfo.DefaultGestures) {
					var existingGesture = new InputGestureCollection(similarInputBinding.DefaultGestures.ToList());
					if(!existingGesture.ContainsTemplateFor(gesture, GestureCompareMode.ExactlyMatches)) {
						similarInputBinding.DefaultGestures.Add(gesture);
					}
				}
				
				similarInputBinding.Categories.AddRange(inputBindingInfo.Categories);
				similarInputBinding.Groups.AddRange(inputBindingInfo.Groups);
			} else {
				inputBidnings.Add(inputBindingInfo);
				inputBindingInfo.IsRegistered = true;
			}
			
			InvokeInputBindingUpdateHandlers(BindingInfoMatchType.SubSet, similarBindingTemplate);
		}
		
		/// <summary>
		/// Unregister input binding
		/// </summary>
		/// <param name="inputBindingInfo">Input binding parameters</param>
		public static void UnregisterInputBinding(BindingInfoMatchType matchType,  params BindingInfoTemplate[] templates)
		{
			foreach(var similarInputBindingInfo in FindInputBindingInfos(matchType, templates).ToArray()) {
				inputBidnings.Remove(similarInputBindingInfo);
				similarInputBindingInfo.RemoveActiveInputBindings();
			}
			
			InvokeInputBindingUpdateHandlers(BindingInfoMatchType.SubSet | BindingInfoMatchType.SuperSet, templates);
		}
		
		public static IEnumerable<InputBindingInfo> FindInputBindingInfos(BindingInfoMatchType matchType, params BindingInfoTemplate[] templates)
		{
			return FindBindingInfos(inputBidnings, matchType, templates).Cast<InputBindingInfo>();
		}
		
		private static IEnumerable<IBindingInfo> FindBindingInfos(ICollection<IBindingInfo> bindingInfos, BindingInfoMatchType matchType, params BindingInfoTemplate[] templates)
		{
			foreach(var binding in bindingInfos) {
				foreach(var template in templates) {
					if(template.IsTemplateFor(binding, matchType)) {
						yield return binding;
						continue;
					}
				}
			}
		}
		
		public static InputBindingCollection FindInputBindings(BindingInfoMatchType matchType, params BindingInfoTemplate[] templates) 
		{
			var inputBindingInfoCollection = FindInputBindingInfos(matchType, templates);
			
			var inputBindingCollection = new InputBindingCollection();
			foreach(var bindingInfo in inputBindingInfoCollection) {
				inputBindingCollection.AddRange(bindingInfo.ActiveInputBindings);
			}
			
			return inputBindingCollection;
		}
		
		/// <summary>
		/// Remove input binding associated with type
		/// </summary>
		/// <param name="ownerType">Owner type</param>
		/// <param name="inputBinding">Input binding</param>
		public static void RemoveClassInputBinding(Type ownerType, InputBinding inputBinding)
		{
			var fieldInfo = typeof(System.Windows.Input.CommandManager).GetField("_classInputBindings", BindingFlags.Static | BindingFlags.NonPublic);
			var fieldData = (HybridDictionary)fieldInfo.GetValue(null);
			var classInputBindings = (InputBindingCollection)fieldData[ownerType];

			if(classInputBindings != null) {
				classInputBindings.Remove(inputBinding);
			}
		}
		
		/// <summary>
		/// Remove command binding associated with type
		/// </summary>
		/// <param name="ownerType"></param>
		/// <param name="commandBinding"></param>
		public static void RemoveClassCommandBinding(Type ownerType, CommandBinding commandBinding) 
		{
			var fieldInfo = typeof(System.Windows.Input.CommandManager).GetField("_classCommandBindings", BindingFlags.Static | BindingFlags.NonPublic);
			var fieldData = (HybridDictionary)fieldInfo.GetValue(null);
			var classCommandBindings = (CommandBindingCollection)fieldData[ownerType];

			if(classCommandBindings != null) {
				classCommandBindings.Remove(commandBinding);
			}
		}
		
		/// <summary>
		/// Register command binding by specifying command binding parameters
		/// </summary>
		/// <param name="commandBindingInfo">Command binding parameters</param>
		public static void RegisterCommandBinding(CommandBindingInfo commandBindingInfo) {
			if(commandBindingInfo.OwnerInstanceName == null && commandBindingInfo.OwnerTypeName == null) {
				throw new ArgumentException("Binding owner must be specified");
			}
			
			if(commandBindingInfo.RoutedCommandName == null) {
				throw new ArgumentException("Routed command name must be specified");
			}
				
			var registeredBindingTemplate = new BindingInfoTemplate();
			registeredBindingTemplate.OwnerInstanceName = commandBindingInfo.OwnerInstanceName;
			registeredBindingTemplate.OwnerTypeName = commandBindingInfo.OwnerTypeName;
			registeredBindingTemplate.RoutedCommandName = commandBindingInfo.RoutedCommandName;
			
			commandBindings.Add(commandBindingInfo);
			commandBindingInfo.IsRegistered = true;
			
			RegisterInputBindingsUpdateHandler(registeredBindingTemplate, commandBindingInfo.DefaultCommandBindingHandler);
			InvokeInputBindingUpdateHandlers(BindingInfoMatchType.SubSet, registeredBindingTemplate);
		}
		
		/// <summary>
		/// Unregister command binding
		/// </summary>
		/// <param name="commandBindingInfo">Command binding parameters</param>
		public static void UnregisterCommandBinding(BindingInfoMatchType matchType,  params BindingInfoTemplate[] templates) {
			foreach(var similarCommandBindingInfo in FindCommandBindingInfos(matchType, templates).ToArray()) {
				commandBindings.Remove(similarCommandBindingInfo);
				similarCommandBindingInfo.RemoveActiveCommandBindings();
			}
			
			InvokeCommandBindingUpdateHandlers(BindingInfoMatchType.SubSet | BindingInfoMatchType.SuperSet, templates);
		}
		
		#region Update handlers

		private static void RegisterBindingsUpdateHandler(Dictionary<IBindingInfo, HashSet<BindingsUpdatedHandler>> bindingUpdateHandlersDictionary, IBindingInfo template, BindingsUpdatedHandler handler) 
		{
			if(!bindingUpdateHandlersDictionary.ContainsKey(template)) {
				bindingUpdateHandlersDictionary.Add(template, new HashSet<BindingsUpdatedHandler>());
			}
			
			bindingUpdateHandlersDictionary[template].Add(handler);
		}
		
		/// <summary>
		/// Register command binding update handler which is triggered when input bindings associated 
		/// with specified type change
		/// </summary>
		/// <param name="ownerTypeName">Owner type name</param>
		/// <param name="handler">Update handler</param>
		public static void RegisterCommandBindingsUpdateHandler(IBindingInfo template, BindingsUpdatedHandler handler) 
		{
			RegisterBindingsUpdateHandler(commandBindingUpdatedHandlers, template, handler);
		}
		
		public static void UnegisterCommandBindingsUpdateHandler(BindingsUpdatedHandler handler, BindingInfoMatchType matchType, params IBindingInfo[] templates) 
		{
			UnregisterBindingsUpdateHandler(commandBindingUpdatedHandlers, handler, matchType, templates);
		}
		
		/// <summary>
		/// Register command binding update handler which is triggered when input bindings associated 
		/// with specified type change
		/// </summary>
		/// <param name="ownerTypeName">Owner type name</param>
		/// <param name="handler">Update handler</param>
		public static void RegisterInputBindingsUpdateHandler(IBindingInfo template, BindingsUpdatedHandler handler) 
		{
			RegisterBindingsUpdateHandler(inputBindingUpdatedHandlers, template, handler);
		}
		
		public static void UnregisterInputBindingsUpdateHandler(BindingsUpdatedHandler handler, BindingInfoMatchType matchType, params IBindingInfo[] templates)
		{
			UnregisterBindingsUpdateHandler(inputBindingUpdatedHandlers, handler, matchType, templates);
		}

		private static void UnregisterBindingsUpdateHandler(Dictionary<IBindingInfo, HashSet<BindingsUpdatedHandler>> updateHandlerDictionary, BindingsUpdatedHandler handler, BindingInfoMatchType matchType, params IBindingInfo[] templates)
		{
			foreach(var updateHandlerPair in updateHandlerDictionary) {
				foreach(var template in templates) {
					if(template.IsTemplateFor(updateHandlerPair.Key, matchType)) {
						if(handler != null) {
							updateHandlerPair.Value.Remove(handler);
						}
					}
				}
			}
		}

		private static void InvokeBindingUpdateHandlers(Dictionary<IBindingInfo, HashSet<BindingsUpdatedHandler>> updateHandlerDictionary, BindingInfoMatchType matchType, params BindingInfoTemplate[] templates)
		{
			foreach(var updateHandlerPair in updateHandlerDictionary) {
				foreach(var template in templates) {
					if(template.IsTemplateFor(updateHandlerPair.Key, matchType)) {
						foreach(var handler in updateHandlerPair.Value) {
							if(handler != null) {
								handler.Invoke();
							}
						}	
					}
				}
			}
		}
		
		public static void InvokeCommandBindingUpdateHandlers(BindingInfoMatchType matchType, params BindingInfoTemplate[] templates)
		{
			InvokeBindingUpdateHandlers(commandBindingUpdatedHandlers, matchType, templates);
		}
		
		public static void InvokeInputBindingUpdateHandlers(BindingInfoMatchType matchType, params BindingInfoTemplate[] templates) 
		{
			InvokeBindingUpdateHandlers(inputBindingUpdatedHandlers, matchType, templates);
		}
		#endregion
		
		/// <summary>
		/// Load all registered commands in add-in
		/// </summary>
		/// <param name="addIn">Add-in</param>
		public static void LoadAddinCommands(AddIn addIn) {		
			foreach(CommandBindingInfo binding in commandBindings) {
				if(binding.AddIn != addIn) continue;
		
				if(binding.CommandTypeName != null && !commands.ContainsKey(binding.CommandTypeName)){
					var command = addIn.CreateObject(binding.CommandTypeName);
					var wpfCommand = command as System.Windows.Input.ICommand;
					if(wpfCommand == null) {
						wpfCommand = new WpfCommandWrapper((ICSharpCode.Core.ICommand)command);
					}
				
					commands.Add(binding.CommandTypeName, wpfCommand);
				}
			}
		}
		
		/// <summary>
		/// Register command object (either instance of <see cref="System.Windows.Input.ICommand" /> or <see cref="ICSharpCode.Core.ICommand" />)
		/// which can be identified by command name
		/// </summary>
		/// <param name="commandName">Command name</param>
		/// <param name="command">Command instance</param>
		public static void LoadCommand(string commandName, object command) {
			var wpfCommand = command as System.Windows.Input.ICommand;
			if(wpfCommand == null) {
				wpfCommand = new WpfCommandWrapper((ICSharpCode.Core.ICommand)command);
			}
			
			if(!commands.ContainsKey(commandName)) {
				commands.Add(commandName, wpfCommand);
			}
		}

		/// <summary>
		/// Get list of all command bindings which satisfy provided parameters
		/// 
		/// Null arguments are ignored
		/// </summary>
		/// <param name="contextName">Context class full name</param>
		/// <param name="contextInstance">Get command bindings assigned only to specific context</param>
		/// <param name="routedCommandName">Context class full name</param>
		/// <param name="className">Context class full name</param>
		/// <returns>Collection of managed command bindings</returns>
		public static IEnumerable<CommandBindingInfo> FindCommandBindingInfos(BindingInfoMatchType matchType, params BindingInfoTemplate[] templates)
		{
			return FindBindingInfos(commandBindings, matchType, templates).Cast<CommandBindingInfo>();
		}

		public static CommandBindingCollection FindCommandBindings(BindingInfoMatchType matchType, params BindingInfoTemplate[] templates) 
		{
			var commandBindingInfoCollection = FindCommandBindingInfos(matchType, templates);
			var commandBindingCollection = new CommandBindingCollection();
			foreach(var bindingInfo in commandBindingInfoCollection) {
				commandBindingCollection.AddRange(bindingInfo.ActiveCommandBindings);
			}
			
			return commandBindingCollection;
		}
		
		/// <summary>
		/// Get list of input gestures from all input bindings which satisfy provided parameters
		/// 
		/// Null arguments are ignored
		/// </summary>
		/// <param name="contextName">Context class full name</param>
		/// <param name="contextInstance">Get gestures assigned only to specific context</param>
		/// <param name="routedCommandName">Routed UI command name</param>
		/// <param name="gesture">Gesture</param>
		public static InputGestureCollection FindInputGestures(BindingInfoMatchType matchType, params BindingInfoTemplate[] templates) {
			var bindings = FindInputBindingInfos(matchType, templates);
			var gestures = new InputGestureCollection();
			
			foreach(InputBindingInfo bindingInfo in bindings) {
				if(bindingInfo.ActiveGestures != null) {
					foreach(InputGesture gesture in bindingInfo.ActiveGestures) {
						if(!gestures.ContainsTemplateFor(gesture, GestureCompareMode.ExactlyMatches)) {
							gestures.Add(gesture);
						}
					}
				}
			}
			
			return gestures;
		}
		
		public static InputBindingCategory GetInputBindingCategory(string categoryPath, bool throwWhenNotFound)
		{
			foreach(var category in InputBindingCategories) {
				if(category.Path == categoryPath) {
					return category;
				}
			}
			
			if(throwWhenNotFound) {
				throw new ApplicationException(string.Format("InputBindingCategory with path {0} was not found", categoryPath));
			}
			
			return null;
		}
		
		public static ICollection<InputBindingCategory> GetInputBindingCategoryCollection(string categoryPathCollectionString, bool throwWhenNotFound)
		{
			var categoryPathCollection = categoryPathCollectionString.Split(',');
			var categories = new List<InputBindingCategory>();
			foreach(var categoryPath in categoryPathCollection) {
				var category = CommandManager.GetInputBindingCategory(categoryPath, throwWhenNotFound);
				
				if(category != null) {
					categories.Add(category);
				}
			}
			
			return categories;
		}
		
		public static IEnumerable<InputBindingCategory> GetInputBindingCategoryChildren(string categoryPath) 
		{
			var categoryDepth = categoryPath.Count(c => c == '/');
			foreach(var currentCategory in InputBindingCategories) {
				if(currentCategory.Path.StartsWith(categoryPath)) {
					var currentCategoryDepth = currentCategory.Path.Count(c => c == '/');
					
					if(currentCategoryDepth == categoryDepth + 1)
					{
		 				yield return currentCategory;
					}
				}
			}
		}
		
		public static void RegisterInputBindingCategory(InputBindingCategory category) 
		{
			if(string.IsNullOrEmpty(category.Path)) {
				throw new ArgumentException("InputBindingCategory path can not be empty");
			}
			
			if(string.IsNullOrEmpty(category.Text)) {
				throw new ArgumentException("InputBindingCategory text can not be empty");
			}
			
			InputBindingCategories.Add(category);
		}
		
	}	
		
	public static class TypeExtensions
	{
		public static string GetShortAssemblyQualifiedName(this Type type)
		{
			return string.Format("{0}, {1}", type.FullName, type.Assembly.GetName().Name);
		}
	}
}
