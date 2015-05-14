﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Microsoft.AspNet.SignalR;
using TaxiFrontend.Hubs;
using TaxiShared;

namespace TaxiFrontend.Actors
{
	public class PresentingActor : ReceiveActor
	{
		private readonly IHubContext _chat;
        private static readonly Dictionary<string, ViewPort> UserBounds = new Dictionary<string, ViewPort>();

		public PresentingActor()
		{
			_chat = GlobalHost.ConnectionManager.GetHubContext<PositionHub>();
			Receive<Taxi.PositionBearing>(p => PositionChanged(p));
			Receive<Publisher.SourceAvailable>(s => SourceChanged(s));
			Receive<UpdatedBounds>(bounds =>
			{
                //create a new viewport for the user
			    UserBounds[bounds.UserId] = new ViewPort(bounds.LatitudeNorthEast, bounds.LongitudeNorthEast,
			        bounds.LatitudeSouthWest, bounds.LongitudeSouthWest, bounds.ZoomLevel);
			});
			Receive<Disconnected>(disconnected => UserBounds.Remove(disconnected.UserId));
		}

		private async Task PositionChanged(Taxi.PositionBearing position)
		{
			var zoomedInUsers = FindUsersSeeingThisVehicle(position);
			await _chat.Clients.Clients(zoomedInUsers).positionChanged(position);

            //TODO: send aggregated events for clients that are zoomed out

            var zoomedOutUsers = FindUsersSeeingThisArea(position);
            //await _chat.Clients.Clients(zoomedInUsers).positionChanged(position);
            //aggregate
		}

        //TODO: inconsistency between messages and methods. 
		private async Task SourceChanged(Publisher.SourceAvailable s)
		{
			await _chat.Clients.All.sourceAdded(s.SourceName);
		}

        private List<string> FindUsersSeeingThisArea(Taxi.PositionBearing position)
        {
            return UserBounds.Where(b => b.Value.ZoomLevel <= 10 && b.Value.Contains(position))
                .Select(b => b.Key)
                .ToList();
        }

		private List<string> FindUsersSeeingThisVehicle(Taxi.PositionBearing position)
		{
			return UserBounds.Where(b => b.Value.ZoomLevel > 10 && b.Value.Contains(position))
				.Select(b => b.Key)
				.ToList();
		}

		public class Disconnected
		{
			public string UserId { get; private set; }

			public Disconnected(string userId)
			{
				UserId = userId;
			}
		}

        public class UpdatedBounds
		{
			public string UserId { get; set; }
			public UpdatedBounds(double latitudeNorthEast, double longitudeNorthEast, double latitudeSouthWest, double longitudeSouthWest,double zoomLevel)
			{
				LatitudeNorthEast = latitudeNorthEast;
				LongitudeNorthEast = longitudeNorthEast;
				LatitudeSouthWest = latitudeSouthWest;
				LongitudeSouthWest = longitudeSouthWest;
			    ZoomLevel = zoomLevel;
			}
			public double LatitudeNorthEast { get; private set; }
			public double LongitudeNorthEast { get; private set; }
			public double LatitudeSouthWest { get; private set; }
			public double LongitudeSouthWest { get; private set; }
		    public double ZoomLevel { get; private set; }
		}
	}

    public class ViewPort
    {
        public ViewPort(double latitudeNorthEast, double longitudeNorthEast, double latitudeSouthWest,
            double longitudeSouthWest, double zoomLevel)
        {
            LatitudeNorthEast = latitudeNorthEast;
            LongitudeNorthEast = longitudeNorthEast;
            LatitudeSouthWest = latitudeSouthWest;
            LongitudeSouthWest = longitudeSouthWest;
            ZoomLevel = zoomLevel;
        }

        public double LatitudeNorthEast { get; private set; }
        public double LongitudeNorthEast { get; private set; }
        public double LatitudeSouthWest { get; private set; }
        public double LongitudeSouthWest { get; private set; }
        public double ZoomLevel { get; private set; }
    }
}