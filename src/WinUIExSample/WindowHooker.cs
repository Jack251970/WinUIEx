// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Win32.SafeHandles;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Power;
using Windows.Win32.UI.WindowsAndMessaging;

namespace WinUIExSample;

/// <summary>
/// A class that hooks into the window procedure of a window to listen for power events.
/// <see href="https://learn.microsoft.com/zh-cn/windows/win32/power/power-setting-guids"/>
/// </summary>
internal partial class WindowHooker : IDisposable
{
    private readonly IntPtr _hwnd;

    private readonly SafeHandle _safeHandle;

    private readonly WNDPROC _originalWndProc;

    private UnregisterPowerSettingNotificationSafeHandle _consoleDisplayStateHandle;

    private UnregisterPowerSettingNotificationSafeHandle _lidSwitchStateChangeHandle;

    internal WindowHooker(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _safeHandle = new SafeAccessTokenHandle(hwnd);

        _consoleDisplayStateHandle = PInvoke.RegisterPowerSettingNotification(
            _safeHandle,
            PInvoke.GUID_CONSOLE_DISPLAY_STATE,
            (uint)REGISTER_NOTIFICATION_FLAGS.DEVICE_NOTIFY_WINDOW_HANDLE);
        _lidSwitchStateChangeHandle = PInvoke.RegisterPowerSettingNotification(
            _safeHandle,
            PInvoke.GUID_LIDSWITCH_STATE_CHANGE,
            (uint)REGISTER_NOTIFICATION_FLAGS.DEVICE_NOTIFY_WINDOW_HANDLE);
        if (_consoleDisplayStateHandle.DangerousGetHandle() == IntPtr.Zero && _lidSwitchStateChangeHandle.DangerousGetHandle() == IntPtr.Zero)
        {
            return;
        }

        /*var wndproc = SetWindowLongPtr(
            new(hwnd),
            WINDOW_LONG_PTR_INDEX.GWL_WNDPROC,
            Marshal.GetFunctionPointerForDelegate<WNDPROC>(WndProc));
        if (wndproc == IntPtr.Zero)
        {
            return;
        }

        _originalWndProc = Marshal.GetDelegateForFunctionPointer<WNDPROC>(wndproc);*/
    }

    public void Dispose()
    {
        /*SetWindowLongPtr(
            new(_hwnd),
            WINDOW_LONG_PTR_INDEX.GWL_WNDPROC,
            Marshal.GetFunctionPointerForDelegate(_originalWndProc));*/

        PInvoke.UnregisterPowerSettingNotification(new HPOWERNOTIFY(_consoleDisplayStateHandle.DangerousGetHandle()));
        PInvoke.UnregisterPowerSettingNotification(new HPOWERNOTIFY(_lidSwitchStateChangeHandle.DangerousGetHandle()));

        _consoleDisplayStateHandle.DangerousRelease();
        _lidSwitchStateChangeHandle.DangerousRelease();
    }

    private LRESULT WndProc(HWND hWnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (msg == PInvoke.WM_POWERBROADCAST && wParam == PInvoke.PBT_POWERSETTINGCHANGE)
        {
            var settings = Marshal.PtrToStructure<POWERBROADCAST_SETTING>(lParam);
            if (settings.PowerSetting == PInvoke.GUID_LIDSWITCH_STATE_CHANGE)
            {
                switch (settings.Data.AsSpan(1)[0])
                {
                    case 0:
                        Debug.WriteLine("Lid closed");
                        break;
                    case 1:
                        Debug.WriteLine("Lid opened");
                        break;
                    default:
                        Debug.WriteLine("Lid unknown state");
                        break;
                }
            }
            else if (settings.PowerSetting == PInvoke.GUID_CONSOLE_DISPLAY_STATE)
            {
                switch (settings.Data.AsSpan(1)[0])
                {
                    case 0:
                        Debug.WriteLine("Monitor Power Off");
                        break;
                    case 1:
                        Debug.WriteLine("Monitor Power On");
                        break;
                    case 2:
                        Debug.WriteLine("Monitor Dimmed");
                        break;
                }
            }
        }

        return PInvoke.CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
    }

    #region Win32

    // CSWin32 will not produce these methods for x86 so we need to define them here.
    [DllImport("user32.dll", ExactSpelling = true, EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    internal static extern nint SetWindowLongPtr64(HWND hWnd, WINDOW_LONG_PTR_INDEX nIndex, nint dwNewLong);

    private static nint SetWindowLongPtr(HWND hWnd, WINDOW_LONG_PTR_INDEX nIndex, nint newLong)
    {
        if (IntPtr.Size == 8)
        {
            return SetWindowLongPtr64(hWnd, nIndex, newLong);
        }
        else
        {
            return PInvoke.SetWindowLong(hWnd, nIndex, (int)newLong);
        }
    }

    #endregion
}
