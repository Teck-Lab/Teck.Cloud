// <copyright file="CustomerServiceVersionResponse.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,AV2305,CA1515,CS1591

namespace Customer.Api.Endpoints.V1.Service;

public sealed record CustomerServiceVersionResponse(string Service, string Version);
