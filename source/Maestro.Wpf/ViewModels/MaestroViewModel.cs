﻿using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Wpf.Messages;
using MediatR;

namespace Maestro.Wpf.ViewModels;

public partial class MaestroViewModel : ObservableObject
{
    readonly IMediator _mediator;
    readonly IClock _clock;
    
    [ObservableProperty]
    ObservableCollection<AirportViewModel> _availableAirports = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenDesequencedWindowCommand))]
    AirportViewModel? _selectedAirport;

    [ObservableProperty]
    RunwayModeViewModel? _selectedRunwayMode;

    [ObservableProperty]
    ViewConfiguration? _selectedView;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Desequenced))]
    List<FlightViewModel> _flights = [];
    
    public string[] Desequenced => Flights.Where(f => f.State == State.Desequenced).Select(airport => airport.Callsign).ToArray();

    public MaestroViewModel(IMediator mediator, IClock clock)
    {
        _mediator = mediator;
        _clock = clock;
    }

    partial void OnAvailableAirportsChanged(ObservableCollection<AirportViewModel> availableAirports)
    {
        // Select the first airport if the selected one no longer exists
        if (SelectedAirport == null || !availableAirports.Any(a => a.Identifier == SelectedAirport.Identifier))
        {
            SelectedAirport = availableAirports.FirstOrDefault();
        }
    }

    partial void OnSelectedAirportChanged(AirportViewModel? airportViewModel)
    {
        if (airportViewModel is null)
        {
            SelectedRunwayMode = null;
            SelectedView = null;
            return;
        }

        if (SelectedRunwayMode == null || airportViewModel.RunwayModes.All(r => r.Identifier != SelectedRunwayMode.Identifier))
        {
            SelectedRunwayMode = airportViewModel.RunwayModes.FirstOrDefault();
        }

        if (SelectedView == null || airportViewModel.Views.All(s => s.Identifier != SelectedView.Identifier))
        {
            SelectedView = airportViewModel.Views.FirstOrDefault();
        }

        var response = _mediator.Send(new GetSequenceRequest(airportViewModel.Identifier)).GetAwaiter().GetResult();
        foreach (var flight in response.Sequence.Flights)
        {
            UpdateFlight(flight);
        }
    }

    [RelayCommand]
    async Task LoadConfiguration()
    {
        var response = await _mediator.Send(new GetAirportConfigurationRequest(), CancellationToken.None);

        AvailableAirports.Clear();

        foreach (var airport in response.Airports)
        {
            var runwayModes = airport.RunwayModes.Select(rm =>
                new RunwayModeViewModel(
                    rm.Identifier,
                    rm.Runways.Select(r =>
                        new RunwayViewModel(r.Identifier, TimeSpan.FromSeconds(r.DefaultLandingRateSeconds)))
                    .ToArray()))
                .ToArray();

            AvailableAirports.Add(new AirportViewModel(airport.Identifier, runwayModes, airport.Views));
        }
    }

    [RelayCommand]
    void SelectView(ViewConfiguration? viewConfiguration)
    {
        SelectedView = viewConfiguration;
    }
    
    [RelayCommand(CanExecute = nameof(CanOpenDesequencedWindow))]
    void OpenDesequencedWindow() => _mediator.Send(new OpenDesequencedWindowRequest(SelectedAirport!.Identifier, Desequenced));
    bool CanOpenDesequencedWindow() => SelectedAirport is not null;

    public void UpdateFlight(FlightMessage flight)
    {
        var flights = Flights.ToList();
        var index = flights.FindIndex(f => f.Callsign == flight.Callsign);
        var viewModel = new FlightViewModel(flight);
        if (index != -1)
        {
            flights[index] = viewModel;
        }
        else
        {
            flights.Add(viewModel);
        }

        Flights = flights;
    }

    partial void OnSelectedRunwayModeChanged(RunwayModeViewModel? runwayMode)
    {
        if (SelectedAirport is null || runwayMode is null)
            return;
        
        // TODO: Ask if runways should be re-assigned
        _mediator.Send(
            new ChangeRunwayModeRequest(
                SelectedAirport.Identifier,
                runwayMode.Identifier,
                _clock.UtcNow(),
                false));
    }
}
