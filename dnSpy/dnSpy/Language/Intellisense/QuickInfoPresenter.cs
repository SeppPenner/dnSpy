﻿/*
    Copyright (C) 2014-2016 de4dot@gmail.com

    This file is part of dnSpy

    dnSpy is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    dnSpy is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with dnSpy.  If not, see <http://www.gnu.org/licenses/>.
*/

using System.Diagnostics;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using dnSpy.Text.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;

namespace dnSpy.Language.Intellisense {
	sealed class QuickInfoPresenter : QuickInfoPresenterBase, ICustomIntellisensePresenter {
		readonly Popup popup;
		readonly IWpfTextView wpfTextView;

		public QuickInfoPresenter(IQuickInfoSession session)
			: base(session) {
			wpfTextView = session.TextView as IWpfTextView;
			Debug.Assert(wpfTextView != null);
			popup = new Popup {
				PlacementTarget = wpfTextView?.VisualElement,
				Placement = PlacementMode.Relative,
				Visibility = Visibility.Collapsed,
				IsOpen = false,
				AllowsTransparency = true,
				UseLayoutRounding = true,
				SnapsToDevicePixels = true,
			};
		}

		bool renderCalled;
		public void Render() {
			Debug.Assert(!renderCalled);
			if (renderCalled)
				return;
			renderCalled = true;
			if (!RenderCore())
				session.Dismiss();
		}

		bool RenderCore() {
			if (session.IsDismissed || session.TextView.IsClosed)
				return false;
			if (wpfTextView == null)
				return false;

			var point = session.GetTriggerPoint(session.TextView.TextSnapshot);
			Debug.Assert(point != null);
			if (point == null)
				return false;

			var line = session.TextView.TextViewLines.GetTextViewLineContainingBufferPosition(point.Value);
			Debug.Assert(line != null && line.VisibilityState != VisibilityState.Unattached);
			if (line == null || line.VisibilityState == VisibilityState.Unattached)
				return false;

			var bounds = line.GetExtendedCharacterBounds(point.Value);
			popup.HorizontalOffset = bounds.Left - wpfTextView.ViewportLeft;
			popup.VerticalOffset = bounds.TextBottom - session.TextView.ViewportTop;

			PopupHelper.SetScaleTransform(wpfTextView, popup);
			popup.Child = control;
			popup.Visibility = Visibility.Visible;
			popup.IsOpen = true;

			wpfTextView.VisualElement.MouseLeave += VisualElement_MouseLeave;
			wpfTextView.VisualElement.MouseMove += VisualElement_MouseMove;
			wpfTextView.Caret.PositionChanged += Caret_PositionChanged;
			wpfTextView.LayoutChanged += TextView_LayoutChanged;
			return true;
		}

		void VisualElement_MouseLeave(object sender, MouseEventArgs e) => session.Dismiss();
		void TextView_LayoutChanged(object sender, TextViewLayoutChangedEventArgs e) => session.Dismiss();
		void Caret_PositionChanged(object sender, CaretPositionChangedEventArgs e) => session.Dismiss();

		void VisualElement_MouseMove(object sender, MouseEventArgs e) {
			if (session.IsDismissed)
				return;
			var mousePos = GetMousePoint(e.MouseDevice);
			if (mousePos == null || !IsMouseWithinSpan(mousePos.Value))
				session.Dismiss();
		}

		Point? GetMousePoint(MouseDevice device) {
			if (wpfTextView == null)
				return null;
			var mousePos = Mouse.PrimaryDevice.GetPosition(wpfTextView.VisualElement);
			mousePos.X += wpfTextView.ViewportLeft;
			mousePos.Y += wpfTextView.ViewportTop;
			return mousePos;
		}

		bool IsMouseWithinSpan(Point mousePos) {
			var applicableToSpan = session.ApplicableToSpan;
			if (applicableToSpan == null)
				return false;
			var span = applicableToSpan.GetSpan(session.TextView.TextSnapshot);
			var lines = session.TextView.TextViewLines.GetTextViewLinesIntersectingSpan(span);
			foreach (var line in lines) {
				foreach (var bounds in line.GetNormalizedTextBounds(span)) {
					if (bounds.Left <= mousePos.X && mousePos.X < bounds.Right && bounds.TextTop <= mousePos.Y && mousePos.Y < bounds.TextBottom)
						return true;
				}
			}
			return false;
		}

		void ClosePopup() {
			popup.IsOpen = false;
			popup.Visibility = Visibility.Collapsed;
			popup.Child = null;
		}

		protected override void OnSessionDismissed() {
			ClosePopup();
			if (wpfTextView != null) {
				wpfTextView.VisualElement.MouseLeave -= VisualElement_MouseLeave;
				wpfTextView.VisualElement.MouseMove -= VisualElement_MouseMove;
				wpfTextView.Caret.PositionChanged -= Caret_PositionChanged;
				wpfTextView.LayoutChanged -= TextView_LayoutChanged;
			}
		}
	}
}
