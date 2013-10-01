using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Xamarin.Auth;
using Xamarin.Social;

namespace Stampsy.Social.Services
{
    public class DisqusManager : ServiceManager
    {
        public override string Name {
            get { return "Disqus"; }
        }

        public override string [] KnownServiceIds {
            get {
                return new [] { "Disqus" };
            }
        }

        public DisqusManager (params Func<Service> [] fallbackChain)
            : base (fallbackChain)
        {
        }

        public static readonly Uri BaseApiUri = new Uri ("https://disqus.com/api/oauth/2.0/");

        public const string ReadWriteScopeKey = "read,write";

        #region Public API

        public override Task<ServiceUser> GetProfileAsync (CancellationToken token = default (CancellationToken), LoginOptions options = default (LoginOptions))
        {
            return this.WithSession (
                () => GetProfile (token),
                options,
                token,
                new [] {
                    ReadWriteScopeKey
                }
            );
        }

        public override Task ShareAsync (Item item, CancellationToken token = default (CancellationToken), LoginOptions options = default (LoginOptions))
        {
            throw new NotImplementedException ();
        }

        public override Task<Page<IEnumerable<ServiceUser>>> GetFriendsAsync (Page<IEnumerable<ServiceUser>> previous = null, CancellationToken token = default (CancellationToken), LoginOptions options = default (LoginOptions))
        {
            throw new NotImplementedException ();
        }

        #endregion

        #region Implementation

        Task<ServiceUser> GetProfile (CancellationToken token)
        {
            var session = EnsureLoggedIn ();
            var request = session.Service.CreateRequest (
                "GET",
                new Uri (BaseApiUri, "users/details.json"),
                session.Account
            );

            return ParseAsync (request, ParseUser, token);
        }

        #endregion

        protected override void HandleResponseException (Exception ex)
        {
            var wex = ex as WebException;
            if (wex == null || wex.Status != WebExceptionStatus.ProtocolError)
                return;

            switch (((HttpWebResponse) wex.Response).StatusCode) {
                case HttpStatusCode.Unauthorized:
                throw new ApiException ("Unauthorized", wex, ApiExceptionKind.Unauthorized);
                case HttpStatusCode.Forbidden:
                throw new ApiException ("Forbidden", wex, ApiExceptionKind.Forbidden);
            }
        }

        protected override void HandleResponseJson (Response response, JToken json)
        {
            if (json.Type != JTokenType.Object)
                return;

            var err = json ["error"];
            if (err == null)
                return;

            var msg = err.Value<string> ("message");
            throw new ApiException (msg, response, ApiExceptionKind.Other);
        }

        protected override string ParsePageToken (JToken json)
        {
            string next = json.Value<string> ("nextPageToken");
            return !string.IsNullOrWhiteSpace (next)
                ? next
                : null;
        }

        protected override ServiceUser ParseUser (JToken user)
        {
            var response = user.Value<JToken> ("response");

            return new ServiceUser {
                Id = response.Value<string> ("id"),
                Name = response.Value<string> ("name"),
                Email = response.Value<string> ("email"),
                //FirstName = response.Value<string> ("name"),
                //LastName = response.Value<string> ("name", "familyName"),
                ImageUrl = response.Value<string> ("avatar", "permalink"),
                //Gender = ParseGender (response.Value<string> ("gender"), "female", "male")
            };
        }
    }
}
