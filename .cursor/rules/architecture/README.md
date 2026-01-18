# 🏗️ Architecture Guidelines

This directory contains rules for maintaining consistent architecture patterns across the Teck.Cloud solution.

## 📋 [Project Structure](project-structure.mdc)

Use this when you're:
- Setting up a new service
- Organizing code within a service
- Creating new building blocks
- Setting up gateways

These rules define:
- Service layer organization (Domain, Application, Infrastructure, Api)
- Building blocks structure
- Gateway (BFF) patterns
- Naming conventions

## 🔄 [Event-Driven Architecture](event-driven-architecture.mdc)

Use this when you're:
- Implementing domain events
- Publishing integration events
- Handling events
- Setting up event handlers

These rules ensure:
- Proper separation between domain and integration events
- Correct event publishing patterns
- Event handler organization
- Async communication between services

## 🌐 [Inter-Service Communication](inter-service-communication.mdc)

Use this when you're:
- Implementing gRPC services
- Setting up HTTP clients
- Configuring gateways
- Making service-to-service calls

These rules define:
- When to use gRPC vs HTTP
- Gateway (BFF) patterns
- Service discovery
- Resilience patterns

## 🗄️ [Data Access Patterns](data-access-patterns.mdc)

Use this when you're:
- Implementing repositories
- Setting up database contexts
- Working with read/write models
- Implementing CQRS

These rules ensure:
- Proper repository patterns
- CQRS separation
- Multi-tenancy support
- Database strategy patterns
