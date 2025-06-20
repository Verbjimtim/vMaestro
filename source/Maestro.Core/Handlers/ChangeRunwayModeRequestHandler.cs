﻿using Maestro.Core.Configuration;
using Maestro.Core.Model;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public record ChangeRunwayModeResponse;

public record ChangeRunwayModeRequest(string AirportIdentifier, string RunwayModeIdentifier, DateTimeOffset StartTime, bool ReAssignRunways) : IRequest<ChangeRunwayModeResponse>;

public class ChangeRunwayModeRequestHandler(ISequenceProvider sequenceProvider, IAirportConfigurationProvider airportConfigurationProvider, ILogger logger)
    : IRequestHandler<ChangeRunwayModeRequest, ChangeRunwayModeResponse>
{
    public async Task<ChangeRunwayModeResponse> Handle(ChangeRunwayModeRequest request, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        
        using var lockedSequence = await sequenceProvider.GetSequence(request.AirportIdentifier, cancellationToken);
        
        var airportConfiguration = airportConfigurationProvider.GetAirportConfigurations().SingleOrDefault(c => c.Identifier == request.AirportIdentifier);
        if (airportConfiguration == null)
        {
            logger.Warning("Airport configuration not found for {AirportIdentifier}.", request.AirportIdentifier);
            return new ChangeRunwayModeResponse();
        }
        
        // TODO: Support changing rates as well as modes
        var runwayMode = airportConfiguration.RunwayModes.SingleOrDefault(r => r.Identifier == request.RunwayModeIdentifier);
        if (runwayMode == null)
        {
            logger.Warning("Runway Mode {RunwayModeIdentifier} not found for {AirportIdentifier}.", request.RunwayModeIdentifier, request.AirportIdentifier);
            return new ChangeRunwayModeResponse();
        }
        
        lockedSequence.Sequence.ChangeRunwayMode(runwayMode);

        if (request.ReAssignRunways)
        {
            throw new NotImplementedException("Reassign runways is not implemented.");
        }

        return new ChangeRunwayModeResponse();
    }
}