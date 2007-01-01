﻿// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Daniel Grunwald" email="daniel@danielgrunwald.de"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using ICSharpCode.WpfDesign.Adorners;

namespace ICSharpCode.WpfDesign.Designer.Controls
{
	/// <summary>
	/// A control that displays adorner panels.
	/// </summary>
	sealed class AdornerLayer : Panel
	{
		#region AdornerPanelCollection
		internal sealed class AdornerPanelCollection : ICollection<AdornerPanel>
		{
			readonly AdornerLayer _layer;
			
			public AdornerPanelCollection(AdornerLayer layer)
			{
				this._layer = layer;
			}
			
			public int Count {
				get { return _layer.Children.Count; }
			}
			
			public bool IsReadOnly {
				get { return false; }
			}
			
			public void Add(AdornerPanel item)
			{
				if (item == null)
					throw new ArgumentNullException("item");
				
				_layer.AddAdorner(item);
			}
			
			public void Clear()
			{
				_layer.ClearAdorners();
			}
			
			public bool Contains(AdornerPanel item)
			{
				if (item == null)
					throw new ArgumentNullException("item");
				
				return VisualTreeHelper.GetParent(item) == _layer;
			}
			
			public void CopyTo(AdornerPanel[] array, int arrayIndex)
			{
				Linq.ToArray(this).CopyTo(array, arrayIndex);
			}
			
			public bool Remove(AdornerPanel item)
			{
				if (item == null)
					throw new ArgumentNullException("item");
				
				return _layer.RemoveAdorner(item);
			}
			
			public IEnumerator<AdornerPanel> GetEnumerator()
			{
				foreach (AdornerPanel panel in _layer.Children) {
					yield return panel;
				}
			}
			
			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			{
				return this.GetEnumerator();
			}
		}
		#endregion
		
		AdornerPanelCollection _adorners;
		readonly UIElement _designPanel;
		
		#if DEBUG_ADORNERLAYER
		int _totalAdornerCount;
		#endif
		
		
		internal AdornerLayer(UIElement designPanel)
		{
			this._designPanel = designPanel;
			
			this.LayoutUpdated += OnLayoutUpdated;
			
			_adorners = new AdornerPanelCollection(this);
		}
		
		void OnLayoutUpdated(object sender, EventArgs e)
		{
			UpdateAllAdorners(false);
//			Debug.WriteLine("Adorner LayoutUpdated. AdornedElements=" + _dict.Count +
//			                ", visible adorners=" + VisualChildrenCount + ", total adorners=" + (_totalAdornerCount));
		}
		
		protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
		{
			base.OnRenderSizeChanged(sizeInfo);
			UpdateAllAdorners(true);
		}
		
		internal AdornerPanelCollection Adorners {
			get {
				return _adorners;
			}
		}
		
		sealed class AdornerInfo
		{
			internal readonly List<AdornerPanel> adorners = new List<AdornerPanel>();
			internal bool isVisible;
		}
		
		// adorned element => AdornerInfo
		Dictionary<UIElement, AdornerInfo> _dict = new Dictionary<UIElement, AdornerInfo>();
		
		void ClearAdorners()
		{
			if (_dict.Count == 0)
				return; // already empty
			
			this.Children.Clear();
			_dict = new Dictionary<UIElement, AdornerInfo>();
			
			#if DEBUG_ADORNERLAYER
			_totalAdornerCount = 0;
			Debug.WriteLine("AdornerLayer cleared.");
			#endif
		}
		
		AdornerInfo GetOrCreateAdornerInfo(UIElement adornedElement)
		{
			AdornerInfo info;
			if (!_dict.TryGetValue(adornedElement, out info)) {
				info = _dict[adornedElement] = new AdornerInfo();
				info.isVisible = adornedElement.IsDescendantOf(_designPanel);
			}
			return info;
		}
		
		AdornerInfo GetExistingAdornerInfo(UIElement adornedElement)
		{
			AdornerInfo info;
			_dict.TryGetValue(adornedElement, out info);
			return info;
		}
		
		void AddAdorner(AdornerPanel adornerPanel)
		{
			if (adornerPanel.AdornedElement == null)
				throw new DesignerException("adornerPanel.AdornedElement must be set");
			
			AdornerInfo info = GetOrCreateAdornerInfo(adornerPanel.AdornedElement);
			info.adorners.Add(adornerPanel);
			
			if (info.isVisible) {
				AddAdornerToChildren(adornerPanel);
			}
			
			#if DEBUG_ADORNERLAYER
			Debug.WriteLine("Adorner added. AdornedElements=" + _dict.Count +
			                ", visible adorners=" + VisualChildrenCount + ", total adorners=" + (++_totalAdornerCount));
			#endif
		}
		
		void AddAdornerToChildren(AdornerPanel adornerPanel)
		{
			UIElementCollection children = this.Children;
			int i = 0;
			for (i = 0; i < children.Count; i++) {
				AdornerPanel p = (AdornerPanel)children[i];
				if (p.Order.CompareTo(adornerPanel.Order) > 0) {
					break;
				}
			}
			children.Insert(i, adornerPanel);
		}
		
		protected override Size MeasureOverride(Size availableSize)
		{
			Size infiniteSize = new Size(double.PositiveInfinity, double.PositiveInfinity);
			foreach (AdornerPanel adorner in this.Children) {
				adorner.Measure(infiniteSize);
			}
			return new Size(0, 0);
		}
		
		protected override Size ArrangeOverride(Size finalSize)
		{
			foreach (AdornerPanel adorner in this.Children) {
				adorner.Arrange(new Rect(new Point(0, 0), adorner.DesiredSize));
				if (adorner.AdornedElement.IsDescendantOf(_designPanel)) {
					adorner.RenderTransform = (Transform)adorner.AdornedElement.TransformToAncestor(_designPanel);
				}
			}
			return finalSize;
		}
		
		bool RemoveAdorner(AdornerPanel adornerPanel)
		{
			if (adornerPanel.AdornedElement == null)
				return false;
			
			AdornerInfo info = GetExistingAdornerInfo(adornerPanel.AdornedElement);
			if (info == null)
				return false;
			
			if (info.adorners.Remove(adornerPanel)) {
				if (info.isVisible) {
					this.Children.Remove(adornerPanel);
				}
				
				if (info.adorners.Count == 0) {
					_dict.Remove(adornerPanel.AdornedElement);
				}
				
				#if DEBUG_ADORNERLAYER
				Debug.WriteLine("Adorner removed. AdornedElements=" + _dict.Count +
				                ", visible adorners=" + VisualChildrenCount + ", total adorners=" + (--_totalAdornerCount));
				#endif
				
				return true;
			} else {
				return false;
			}
		}
		
		public void UpdateAdornersForElement(UIElement element, bool forceInvalidate)
		{
			AdornerInfo info = GetExistingAdornerInfo(element);
			if (info != null) {
				UpdateAdornersForElement(element, info, forceInvalidate);
			}
		}
		
		void UpdateAdornersForElement(UIElement element, AdornerInfo info, bool forceInvalidate)
		{
			if (element.IsDescendantOf(_designPanel)) {
				if (!info.isVisible) {
					info.isVisible = true;
					// make adorners visible:
					info.adorners.ForEach(AddAdornerToChildren);
				}
				if (forceInvalidate) {
					foreach (AdornerPanel p in info.adorners) {
						p.InvalidateMeasure();
					}
				}
			} else {
				if (info.isVisible) {
					info.isVisible = false;
					// make adorners invisible:
					info.adorners.ForEach(this.Children.Remove);
				}
			}
		}
		
		void UpdateAllAdorners(bool forceInvalidate)
		{
			foreach (KeyValuePair<UIElement, AdornerInfo> pair in _dict) {
				UpdateAdornersForElement(pair.Key, pair.Value, forceInvalidate);
			}
		}
	}
}
