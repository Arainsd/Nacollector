﻿using CefSharp;
using CefSharp.WinForms;
using Nacollector.Browser.Handler;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NacollectorUtils;
using Nacollector.Ui;

namespace Nacollector.Browser
{
    public class CrBrowser
    {
        private MainForm form;
        private ChromiumWebBrowser browser;

        public CrBrowser(MainForm form, string address)
        {
            this.form = form;

            // 初始化浏览器
            browser = new ChromiumWebBrowser(address);

            // BrowserSettings 必须在 Controls.Add 之前
            BrowserSettings browserSettings = new BrowserSettings
            {
                // FileAccessFromFileUrls 必须 Enabled
                // 不然 AJAX 请求 file:// 会显示 
                // Cross origin requests are only supported for protocol schemes: http, data, chrome, chrome-extension, https.
                FileAccessFromFileUrls = CefState.Enabled,
                UniversalAccessFromFileUrls = CefState.Enabled,
                DefaultEncoding = "UTF-8",
                BackgroundColor = (uint)ColorTranslator.FromHtml("#21252b").ToArgb()
            };
            browserSettings.WebSecurity = CefState.Disabled;
            browser.BrowserSettings = browserSettings;
            
            browser.MenuHandler = new MenuHandler(this);
            browser.LifeSpanHandler = new LifeSpanHandler();
            browser.LoadHandler = new LoadHandler();
            browser.DragHandler = new DragDropHandler();
            ((DragDropHandler)browser.DragHandler).Enable = false;

            browser.FrameLoadEnd += new EventHandler<FrameLoadEndEventArgs>(Browser_onFrameLoadEnd);
            browser.IsBrowserInitializedChanged += new EventHandler<IsBrowserInitializedChangedEventArgs>(Browser_onIsBrowserInitializedChanged);
        }

        // Frame 加载完毕时执行
        private void Browser_onFrameLoadEnd(object _sender, FrameLoadEndEventArgs e)
        {
            ChromiumWebBrowser sender = (ChromiumWebBrowser)_sender;
            string url = e.Frame.Url;
            if (url.IndexOf("http://127.0.0.1") == 0 || url.IndexOf("nacollector://") == 0)
            {
                ((DragDropHandler)browser.DragHandler).Enable = true;
            }
            Debug.WriteLine(url);
        }
        
        // 浏览器初始化完毕时执行
        private void Browser_onIsBrowserInitializedChanged(object sender, IsBrowserInitializedChangedEventArgs args)
        {
            if (args.IsBrowserInitialized)
            {
                // 监听 CefSharp 的 Windows Message
                ChromeWidgetMessageInterceptor.SetupLoop(browser, (message) =>
                {
                    var dragHandler = (DragDropHandler)browser.DragHandler;
                    if (!dragHandler.Enable)
                    {
                        return;
                    }

                    Point point = new Point(message.LParam.ToInt32());

                    // 指定区域拖拽
                    if (dragHandler.draggableRegion != null && dragHandler.draggableRegion.IsVisible(point))
                    {
                        // 若现在鼠标指针在可拖动区域内
                        if (message.Msg == (int)WindowMessages.WM_LBUTTONDBLCLK) // 鼠标左键双击
                        {
                            form.BeginInvoke((MethodInvoker)delegate
                            {
                                form.ToggleMaximize();
                            });
                        }
                        else if (message.Msg == (int)WindowMessages.WM_LBUTTONDOWN) // 鼠标左键按下
                        {
                            form.BeginInvoke((MethodInvoker)delegate
                            {
                                NativeMethods.ReleaseCapture();
                                NativeMethods.SendMessage(form.Handle, (int)WindowMessages.WM_NCLBUTTONDOWN, (int)HitTestValues.HTCAPTION, 0); // 执行 模拟标题栏拖动
                            });
                        }
                        else if (message.Msg == (int)WindowMessages.WM_RBUTTONDOWN) // 鼠标右键按下
                        {
                            form.BeginInvoke((MethodInvoker)delegate
                            {
                                form.ShowSystemMenu(point);
                            });
                        }
                    }
                    else
                    {
                        // 拖拽改变窗口大小
                        const uint HTLEFT = 10;
                        const uint HTRIGHT = 11;
                        const uint HTBOTTOMRIGHT = 17;
                        const uint HTBOTTOM = 15;
                        const uint HTBOTTOMLEFT = 16;
                        const uint HTTOP = 12;
                        const uint HTTOPLEFT = 13;
                        const uint HTTOPRIGHT = 14;

                        const int RESIZE_HANDLE_SIZE = 5;

                        if (message.Msg == (int)WindowMessages.WM_MOUSEMOVE || message.Msg == (int)WindowMessages.WM_LBUTTONDOWN)
                        {
                            form.BeginInvoke((MethodInvoker)delegate
                            {
                                Size formSize = form.Size;
                                //Point screenPoint = new Point(message.LParam.ToInt32());
                                //Point clientPoint = form.PointToClient(screenPoint);

                                Dictionary<uint, Rectangle> boxes = new Dictionary<uint, Rectangle>() {
            {HTBOTTOMLEFT, new Rectangle(0, formSize.Height - RESIZE_HANDLE_SIZE, RESIZE_HANDLE_SIZE, RESIZE_HANDLE_SIZE)},
            {HTBOTTOM, new Rectangle(RESIZE_HANDLE_SIZE, formSize.Height - RESIZE_HANDLE_SIZE, formSize.Width - 2*RESIZE_HANDLE_SIZE, RESIZE_HANDLE_SIZE)},
            {HTBOTTOMRIGHT, new Rectangle(formSize.Width - RESIZE_HANDLE_SIZE, formSize.Height - RESIZE_HANDLE_SIZE, RESIZE_HANDLE_SIZE, RESIZE_HANDLE_SIZE)},
            {HTRIGHT, new Rectangle(formSize.Width - RESIZE_HANDLE_SIZE, RESIZE_HANDLE_SIZE, RESIZE_HANDLE_SIZE, formSize.Height - 2*RESIZE_HANDLE_SIZE)},
            {HTTOPRIGHT, new Rectangle(formSize.Width - RESIZE_HANDLE_SIZE, 0, RESIZE_HANDLE_SIZE, RESIZE_HANDLE_SIZE) },
            {HTTOP, new Rectangle(RESIZE_HANDLE_SIZE, 0, formSize.Width - 2*RESIZE_HANDLE_SIZE, RESIZE_HANDLE_SIZE) },
            {HTTOPLEFT, new Rectangle(0, 0, RESIZE_HANDLE_SIZE, RESIZE_HANDLE_SIZE) },
            {HTLEFT, new Rectangle(0, RESIZE_HANDLE_SIZE, RESIZE_HANDLE_SIZE, formSize.Height - 2*RESIZE_HANDLE_SIZE) }
        };

                                Dictionary<uint, Cursor> cursors = new Dictionary<uint, Cursor>() {
            {HTBOTTOMLEFT, Cursors.SizeNESW },
            {HTBOTTOM, Cursors.SizeNS },
            {HTBOTTOMRIGHT, Cursors.SizeNWSE},
            {HTRIGHT, Cursors.SizeWE },
            {HTTOPRIGHT, Cursors.SizeNESW },
            {HTTOP, Cursors.SizeNS },
            {HTTOPLEFT, Cursors.SizeNWSE },
            {HTLEFT, Cursors.SizeWE }
        };

                                // 判断此刻指针是否在 boxes 内
                                foreach (KeyValuePair<uint, Rectangle> hitBox in boxes)
                                {
                                    if (hitBox.Value.Contains(point))
                                    {
                                        browser.Cursor = cursors[hitBox.Key]; // 设置指针图标
                                        if (message.Msg == (int)WindowMessages.WM_LBUTTONDOWN)
                                        {
                                            NativeMethods.ReleaseCapture();
                                            NativeMethods.SendMessage(form.Handle, (int)WindowMessages.WM_NCLBUTTONDOWN, (int)hitBox.Key, 0);
                                        }
                                        break;
                                    }
                                }
                            });

                            return;
                        }
                    }
                });
            }
        }

        /// <summary>
        /// 获取 ChromiumWebBrowser this.browser
        /// </summary>
        /// <returns></returns>
        public ChromiumWebBrowser GetBrowser()
        {
            return browser;
        }

        /// <summary>
        /// 浏览器执行JS代码
        /// </summary>
        /// <param name="jsCodeStr"></param>
        public void RunJS(string jsCodeStr)
        {
            form.BeginInvoke((MethodInvoker)delegate
            {
                browser.ExecuteScriptAsync(jsCodeStr);
            });
        }

        /// <summary>
        /// 浏览器执行JS代码获取返回值
        /// </summary>
        /// <param name="script"></param>
        /// <param name="defaultValue"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        /// <example>同步：EvaluateScript("5555 * 19999 + 88888", 0, TimeSpan.FromSeconds(3)).GetAwaiter().GetResult();</example>
        public async Task<object> EvaluateScript(string script, object defaultValue, TimeSpan timeout)
        {
            object result = defaultValue;
            if (browser.IsBrowserInitialized && !browser.IsDisposed && !browser.Disposing)
            {
                try
                {
                    var task = browser.EvaluateScriptAsync(script, timeout);
                    await task.ContinueWith(res => {
                        if (!res.IsFaulted)
                        {
                            var response = res.Result;
                            result = response.Success ? (response.Result ?? "null") : response.Message;
                        }
                    }).ConfigureAwait(false); // <-- This makes the task to synchronize on a different context
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.InnerException.Message);
                }
            }
            return result;
        }

        public void DownloadUrl(string url)
        {
            var cefBrowser = browser.GetBrowser();
            IBrowserHost ibwhost = cefBrowser == null ? null : cefBrowser.GetHost();
            ibwhost.StartDownload(url);
        }
    }
}