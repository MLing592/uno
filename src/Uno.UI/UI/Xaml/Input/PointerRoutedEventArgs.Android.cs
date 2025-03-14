﻿using System;
using System.Collections.Generic;
using System.Text;
using Android.Views;
using Uno.UI;
using Windows.Foundation;
using Windows.System;
using Microsoft.UI.Xaml.Extensions;
using Android.OS;
using Uno.Extensions;
using Uno.UI.Xaml.Core;
using Uno.UI.Xaml.Input;


#if HAS_UNO_WINUI
using Microsoft.UI.Input;
#else
using Windows.UI.Input;
using Windows.Devices.Input;
#endif

namespace Microsoft.UI.Xaml.Input
{
	partial class PointerRoutedEventArgs
	{
		/// <summary>
		/// The stylus is pressed while holding the barrel button
		/// </summary>
		internal const MotionEventActions StylusWithBarrelDown = (MotionEventActions)211;
		/// <summary>
		/// The stylus is moved after having been pressed while holding the barrel button
		/// </summary>
		internal const MotionEventActions StylusWithBarrelMove = (MotionEventActions)213;
		/// <summary>
		/// The stylus is released after having been pressed while holding the barrel button
		/// </summary>
		internal const MotionEventActions StylusWithBarrelUp = (MotionEventActions)212;

		private const int _pointerIdsCount = (int)MotionEventActions.PointerIndexMask >> (int)MotionEventActions.PointerIndexShift; // 0xff
		private const int _pointerIdsShift = 31 - (int)MotionEventActions.PointerIndexShift; // 23

		// _lastNativeEvent.lastArgs is not necessary equal to LastPointerEvent. _lastNativeEvent.lastArgs is
		// the last PointerRoutedEventArgs that was created as part of the native bubbling of _lastNativeEvent.nativeEvent.
		// In other words, if a PointerRoutedEventArgs was created in managed (using the parameterless constructor),
		// then _lastNativeEvent.lastArgs and LastPointerEvent will diverge.
		private static (MotionEvent nativeEvent, PointerRoutedEventArgs lastArgs)? _lastNativeEvent;

		private readonly int _pointerIndex;
		private readonly ulong _timestamp;
		private readonly float _x;
		private readonly float _y;
		private readonly UIElement _receiver;
		private readonly PointerPointProperties _properties;

		internal bool HasPressedButton => _properties.HasPressedButton;

		internal PointerRoutedEventArgs(MotionEvent nativeEvent, int pointerIndex, UIElement originalSource, UIElement receiver) : this()
		{
			_pointerIndex = pointerIndex;
			_receiver = receiver;

			// NOTE: do not keep a reference to nativeEvent, which will be reused by android's native event bubbling and will be mutated as it
			// goes up through the visual tree. Instead, get whatever values you need here and keep them in fields.
			_timestamp = ToTimeStamp(nativeEvent.EventTime);
			_x = nativeEvent.GetX(pointerIndex);
			_y = nativeEvent.GetY(pointerIndex);

			// Here we assume that usually pointerId is 'PointerIndexShift' bits long (8 bits / 255 ids),
			// and that usually the deviceId is [0, something_not_too_big_hopefully_less_than_0x00ffffff].
			// If deviceId is greater than 0x00ffffff, we might have a conflict but only in case of multi touch
			// and with a high variation of deviceId. We assume that's safe enough.
			// Note: Make sure to use the GetPointerId in order to make sure to keep the same id while: down_1 / down_2 / up_1 / up_2
			//		 otherwise up_2 will be with the id of 1
			var pointerId = ((uint)nativeEvent.GetPointerId(pointerIndex) & _pointerIdsCount) << _pointerIdsShift | (uint)nativeEvent.DeviceId;
			var nativePointerAction = nativeEvent.Action;
			var nativePointerButtons = nativeEvent.ButtonState;
			var nativePointerType = nativeEvent.GetToolType(_pointerIndex);
			var pointerType = nativePointerType.ToPointerDeviceType();
			var basePointerType = (PointerDeviceType)pointerType;
			var isInContact = IsInContact(nativeEvent, basePointerType, nativePointerAction, nativePointerButtons);
			var keys = nativeEvent.MetaState.ToVirtualKeyModifiers();

			FrameId = (uint)nativeEvent.EventTime;
			Pointer = new Pointer(pointerId, basePointerType, isInContact, isInRange: true);
			KeyModifiers = keys;
			OriginalSource = originalSource;

			// On platforms with managed pointers, we reuse the same PointerRoutedEventArgs instance
			// as we bubble up the event in the visual tree. On Android, the event bubbling is done
			// natively and we create a corresponding (new) managed PointerRoutedEventArgs instance
			// for each element up the tree. This means that parents won't see modifications in the
			// PointerRoutedEventArgs instance that were done by the children. We have to detect
			// this and copy the relevant fields ourselves.
			if (_lastNativeEvent?.nativeEvent == nativeEvent)
			{
				GestureEventsAlreadyRaised = _lastNativeEvent.Value.lastArgs.GestureEventsAlreadyRaised;
			}
			_lastNativeEvent = (nativeEvent, this);

			var inputManager = VisualTree.GetContentRootForElement(originalSource)?.InputManager;
			if (inputManager is not null)
			{
				inputManager.LastInputDeviceType = basePointerType switch
				{
					PointerDeviceType.Mouse => InputDeviceType.Mouse,
					PointerDeviceType.Pen => InputDeviceType.Pen,
					_ => InputDeviceType.Touch
				};
			}

			_properties = GetProperties(nativeEvent, nativePointerType, nativePointerAction, nativePointerButtons); // Last: we need the Pointer property to be set!
		}

		public PointerPoint GetCurrentPoint(UIElement relativeTo)
		{
			var device = global::Windows.Devices.Input.PointerDevice.For((global::Windows.Devices.Input.PointerDeviceType)Pointer.PointerDeviceType);
			var (rawPosition, position) = GetPositions(relativeTo);

			return new PointerPoint(FrameId, _timestamp, device, Pointer.PointerId, rawPosition, position, Pointer.IsInContact, _properties);
		}

		private (Point raw, Point relative) GetPositions(UIElement relativeTo)
		{
			Point raw, relative;
			if (relativeTo == null) // Relative to the window
			{
				var windowToReceiver = new int[2];
				_receiver.GetLocationInWindow(windowToReceiver);

				relative = new Point(_x + windowToReceiver[0], _y + windowToReceiver[1]).PhysicalToLogicalPixels();
			}
			else if (relativeTo == _receiver) // Fast path
			{
				relative = new Point(_x, _y).PhysicalToLogicalPixels();
			}
			else
			{
				var posRelToReceiver = new Point(_x, _y).PhysicalToLogicalPixels();
				var posRelToTarget = UIElement.GetTransform(from: _receiver, to: relativeTo).Transform(posRelToReceiver);

				relative = posRelToTarget;
			}

			// Raw coordinates are relative to the screen (easier for the gesture recognizer to track fingers for manipulations)
			// if (ANDROID > 10)
			// {
			//		var raw = new Point(_nativeEvent.getRawX(_pointerIndex), _nativeEvent.getRawY(_pointerIndex)).PhysicalToLogicalPixels();
			// }
			// else
			{
				var screenToReceiver = new int[2];
				_receiver.GetLocationOnScreen(screenToReceiver);

				raw = new Point(_x + screenToReceiver[0], _y + screenToReceiver[1]).PhysicalToLogicalPixels();
			}

			return (raw, relative);
		}

		private PointerPointProperties GetProperties(MotionEvent nativeEvent, MotionEventToolType type, MotionEventActions action, MotionEventButtonState buttons)
		{
			var props = new PointerPointProperties
			{
				IsPrimary = true,
				IsInRange = Pointer.IsInRange
			};

			var isDown = action == /* 0 = */ MotionEventActions.Down || action.HasFlag(MotionEventActions.PointerDown);
			var isUp = action.HasFlag(MotionEventActions.Up) || action.HasFlag(MotionEventActions.PointerUp);
			var updates = _none;
			switch (type)
			{
				case MotionEventToolType.Finger:
				case MotionEventToolType.Unknown: // used by Xamarin.UITest
					props.IsLeftButtonPressed = Pointer.IsInContact;
					updates = isDown ? _fingerDownUpdates : isUp ? _fingerUpUpdates : _none;
					// Pressure = .5f => Keeps default as UWP returns .5 for fingers.
					break;

				case MotionEventToolType.Mouse:
					props.IsLeftButtonPressed = buttons.HasFlag(MotionEventButtonState.Primary);
					props.IsMiddleButtonPressed = buttons.HasFlag(MotionEventButtonState.Tertiary);
					props.IsRightButtonPressed = buttons.HasFlag(MotionEventButtonState.Secondary);
					updates = isDown ? _mouseDownUpdates : isUp ? _mouseUpUpdates : _none;
					// Pressure = .5f => Keeps default as UWP returns .5 for Mouse no matter is button is pressed or not (Android return 1.0 while pressing a button, but 0 otherwise).
					break;

				// Note: On UWP, if you touch screen while already holding the barrel button, you will get a right + barrel,
				//		 ** BUT ** if you touch screen and THEN press the barrel button props will be left + barrel until released.
				//		 On Android this distinction seems to be flagged by the "1101 ****" action flag (i.e. "StylusWithBarrel***" actions),
				//		 so here we set the Is<Left|Right>ButtonPressed based on the action and we don't try to link it to the barrel button state.
				case MotionEventToolType.Stylus when action == StylusWithBarrelDown:
				case MotionEventToolType.Stylus when action == StylusWithBarrelMove:
				case MotionEventToolType.Stylus when action == StylusWithBarrelUp:
					// Note: We still validate the "IsButtonPressed(StylusPrimary)" as the user might release the button while pressed.
					//		 In that case we will still receive moves and up with the "StylusWithBarrel***" actions.
					props.IsBarrelButtonPressed = buttons.HasFlag(MotionEventButtonState.StylusPrimary);
					props.IsRightButtonPressed = Pointer.IsInContact;
					props.Pressure = Math.Min(1f, nativeEvent.GetPressure(_pointerIndex)); // Might exceed 1.0 on Android
					break;
				case MotionEventToolType.Stylus:
					props.IsBarrelButtonPressed = buttons.HasFlag(MotionEventButtonState.StylusPrimary);
					props.IsLeftButtonPressed = Pointer.IsInContact;
					props.Pressure = Math.Min(1f, nativeEvent.GetPressure(_pointerIndex)); // Might exceed 1.0 on Android
					break;
				case MotionEventToolType.Eraser:
					props.IsEraser = true;
					props.Pressure = Math.Min(1f, nativeEvent.GetPressure(_pointerIndex)); // Might exceed 1.0 on Android
					break;

				default:
					break;
			}

			if (Android.OS.Build.VERSION.SdkInt >= BuildVersionCodes.M // ActionButton was introduced with API 23 (https://developer.android.com/reference/android/view/MotionEvent.html#getActionButton())
				&& updates.TryGetValue(nativeEvent.ActionButton, out var update))
			{
				props.PointerUpdateKind = update;
			}

			return props;
		}

		#region Misc static helpers
		private static readonly Dictionary<MotionEventButtonState, PointerUpdateKind> _none = new(0);
		private static readonly Dictionary<MotionEventButtonState, PointerUpdateKind> _fingerDownUpdates = new()
		{
			{ 0, PointerUpdateKind.LeftButtonPressed },
			{ MotionEventButtonState.Primary, PointerUpdateKind.LeftButtonPressed }
		};
		private static readonly Dictionary<MotionEventButtonState, PointerUpdateKind> _fingerUpUpdates = new()
		{
			{ 0, PointerUpdateKind.LeftButtonReleased },
			{ MotionEventButtonState.Primary, PointerUpdateKind.LeftButtonReleased }
		};
		private static readonly Dictionary<MotionEventButtonState, PointerUpdateKind> _mouseDownUpdates = new()
		{
			{ MotionEventButtonState.Primary, PointerUpdateKind.LeftButtonPressed },
			{ MotionEventButtonState.Tertiary, PointerUpdateKind.MiddleButtonPressed },
			{ MotionEventButtonState.Secondary, PointerUpdateKind.RightButtonPressed }
		};
		private static readonly Dictionary<MotionEventButtonState, PointerUpdateKind> _mouseUpUpdates = new()
		{
			{ MotionEventButtonState.Primary, PointerUpdateKind.LeftButtonReleased },
			{ MotionEventButtonState.Tertiary, PointerUpdateKind.MiddleButtonReleased },
			{ MotionEventButtonState.Secondary, PointerUpdateKind.RightButtonReleased }
		};

		private static readonly ulong _unixEpochMs = (ulong)(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc) - new DateTime()).TotalMilliseconds;

		private static ulong ToTimeStamp(long uptimeMillis)
		{
			// Timestamp is in microseconds
			if (FeatureConfiguration.PointerRoutedEventArgs.AllowRelativeTimeStamp)
			{
				return (ulong)(uptimeMillis * 1000);
			}
			else
			{
				// We cannot cache the "bootTime" as the "uptimeMillis" is frozen while in deep sleep
				// (cf. https://developer.android.com/reference/android/os/SystemClock)

				var sleepTime = Android.OS.SystemClock.ElapsedRealtime() - Android.OS.SystemClock.UptimeMillis();
				var realUptime = (ulong)(uptimeMillis + sleepTime);
				return realUptime * 1000;
			}
		}

		private static bool IsInContact(MotionEvent nativeEvent, PointerDeviceType pointerType, MotionEventActions action, MotionEventButtonState buttons)
		{
			switch (pointerType)
			{
				case PointerDeviceType.Mouse:
					// For mouse, we cannot only rely on action: We will get a "HoverExit" when we press the left button.
					return buttons != 0;

				case PointerDeviceType.Pen:
					return nativeEvent.GetAxisValue(Axis.Distance, nativeEvent.ActionIndex) == 0;

				default:
				case PointerDeviceType.Touch:
					// WARNING: MotionEventActions.Down == 0, so action.HasFlag(MotionEventActions.Up) is always true!
					return !action.HasFlag(MotionEventActions.Up)
						&& !action.HasFlag(MotionEventActions.PointerUp)
						&& !action.HasFlag(MotionEventActions.Cancel);
			}
		}
		#endregion
	}
}
