﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Owin;
using Moq;
using Newtonsoft.Json;
using NuGetGallery.Authentication;

namespace NuGetGallery
{
    public static class TestExtensionMethods
    {
        /// <summary>
        /// Should only be used in the rare cases where you are testing an action that
        /// does NOT use AppController.GetCurrentUser()! In those cases, use
        /// TestExtensionMethods.SetCurrentUser(AppController, User) instead.
        /// </summary>
        public static void SetOwinContextCurrentUser(this AppController self, User user, Credential credential = null)
        {
            ClaimsIdentity identity = null;

            if (credential != null)
            {
                identity = AuthenticationService.CreateIdentity(
                    user,
                    AuthenticationTypes.ApiKey,
                    new Claim(NuGetClaims.ApiKey, credential.Value),
                    new Claim(NuGetClaims.Scope, JsonConvert.SerializeObject(credential.Scopes, Formatting.None)));
            }
            else
            {
                identity = new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.Name, string.IsNullOrEmpty(user.Username) ? "theUserName" : user.Username) });
            }

            var principal = new ClaimsPrincipal(identity);

            var mock = Mock.Get(self.HttpContext);
            mock.Setup(c => c.Request.IsAuthenticated).Returns(true);
            mock.Setup(c => c.User).Returns(principal);

            self.OwinContext.Request.User = principal;
        }

        public static void SetCurrentUser(this AppController self, User user, Credential credential = null)
        {
            SetOwinContextCurrentUser(self, user, credential);
            self.OwinContext.Environment[Constants.CurrentUserOwinEnvironmentKey] = user;
        }

        public static async Task<byte[]> CaptureBody(this IOwinResponse self, Func<Task> captureWithin)
        {
            var strm = new MemoryStream();
            self.Body = strm;
            await captureWithin();
            return strm.ToArray();
        }

        public static async Task<T> CaptureBody<T>(this IOwinResponse self, Func<Task> captureWithin, Func<byte[], Task<T>> converter)
        {
            var data = await CaptureBody(self, captureWithin);
            return await converter(data);
        }

        public static Task<string> CaptureBodyAsString(this IOwinResponse self, Func<Task> captureWithin)
        {
            return CaptureBody<string>(self, captureWithin, bytes => Task.FromResult(Encoding.UTF8.GetString(bytes)));
        }
    }
}
