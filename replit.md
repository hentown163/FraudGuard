# Global Payment Fraud Detection System

## Overview

A real-time payment fraud detection system built with ASP.NET Core 8.0 that analyzes transactions from multiple payment gateways (Stripe, PayPal) using machine learning models and behavioral analytics. The system provides sub-300ms fraud scoring, real-time alerting, and an admin dashboard for transaction monitoring and manual review workflows.

## User Preferences

Preferred communication style: Simple, everyday language.

## System Architecture

### Frontend Architecture
- **Framework**: ASP.NET Core 8.0 Razor Pages with Bootstrap 5
- **Real-time Updates**: SignalR for live dashboard updates
- **Visualization**: Chart.js for fraud trend analytics
- **Client Libraries**: jQuery for DOM manipulation and AJAX interactions

**Rationale**: Razor Pages provides a simpler page-focused model compared to MVC for the admin dashboard use case, while SignalR enables real-time transaction monitoring without polling overhead.

### Backend Architecture
- **Web Framework**: ASP.NET Core 8.0 (Razor Pages + Web API)
- **Machine Learning**: ONNX Runtime for in-memory fraud model inference
- **Event Processing**: Event-driven architecture with Azure Event Hubs for transaction streaming

**Rationale**: ONNX allows cross-platform ML model deployment with high performance (<300ms inference time). Event Hubs provides high-throughput ingestion (millions of events/sec) for real-time fraud detection at scale.

### Data Storage Solutions
- **Azure Cosmos DB**: User behavioral profiles and transaction history
  - **Rationale**: Global distribution, low-latency reads (<10ms), and flexible schema for evolving fraud patterns
  
- **Azure Service Bus**: Fraud alert queue management
  - **Rationale**: Reliable message delivery with dead-letter queues for failed alert processing

### Authentication & Security
- **Azure Key Vault**: Secure storage for API keys, secrets, and configuration
  - Stripe API keys
  - PayPal credentials
  - Twilio authentication tokens
  - Database connection strings

**Rationale**: Centralized secret management with automatic rotation support and audit logging, avoiding hardcoded credentials.

### Fraud Detection Pipeline
1. **Transaction Ingestion**: Payment gateway webhooks → Azure Event Hubs
2. **Real-time Scoring**: ONNX model inference with behavioral features
3. **Risk Assessment**: Device fingerprinting + IP geolocation (MaxMind GeoIP2) + velocity checks
4. **Alert Routing**: High-risk transactions → Azure Service Bus → Notification services
5. **Response Time**: Target <300ms end-to-end processing

**Rationale**: Separating ingestion (Event Hubs) from processing allows independent scaling and resilience. ONNX in-memory inference avoids external API latency.

### Integration Patterns
- **Payment Gateways**: Webhook-based event processing
  - Stripe: Webhook signature validation with `Stripe.net` SDK
  - PayPal: REST API integration with OAuth 2.0
  
- **Geolocation**: MaxMind GeoIP2 database for IP-based risk scoring
  - **Rationale**: Local database queries avoid API rate limits and provide consistent <1ms lookups

- **Notifications**: Multi-channel alerting
  - SMS: Twilio for critical fraud alerts
  - Email: Built-in SMTP for lower-priority notifications

## External Dependencies

### Azure Cloud Services
- **Azure Cosmos DB**: NoSQL database for user profiles and transaction logs
- **Azure Event Hubs**: Event streaming platform for transaction ingestion
- **Azure Service Bus**: Message queue for fraud alert delivery
- **Azure Key Vault**: Secret and configuration management

### Payment Gateways
- **Stripe**: Payment processing with webhook event subscriptions
- **PayPal**: Payment processing with REST API integration

### Third-Party Services
- **MaxMind GeoIP2**: IP geolocation database for location-based fraud detection
- **Twilio**: SMS notification delivery for fraud alerts
- **ONNX Runtime**: Machine learning model inference engine

### NuGet Packages
- `Azure.Identity`: Azure authentication
- `Azure.Messaging.EventHubs`: Event streaming
- `Azure.Messaging.ServiceBus`: Message queuing
- `Azure.Security.KeyVault.Secrets`: Secret management
- `Microsoft.Azure.Cosmos`: Cosmos DB SDK
- `Microsoft.ML.OnnxRuntime`: ML model inference
- `Stripe.net`: Stripe API client
- `MaxMind.GeoIP2`: Geolocation library
- `Twilio`: SMS notification client
- `Microsoft.AspNetCore.SignalR`: Real-time communication

### Configuration Requirements
- Fraud detection threshold: Configurable (default 0.7)
- ONNX model path: `wwwroot/onnx/fraud_model.onnx`
- GeoIP database: External MaxMind database file
- Connection strings and API keys: Managed via Azure Key Vault