# Global Payment Fraud Detection System

## Overview

A real-time payment fraud detection system built with ASP.NET Core 8.0 that analyzes transactions from multiple payment gateways (Braintree, Authorize.Net, Stripe, PayPal) using machine learning models, third-party fraud detection (Sift Science), and behavioral analytics. The system provides sub-300ms fraud scoring, automatic gateway failover, real-time alerting, and an admin dashboard for transaction monitoring and manual review workflows.

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
- **Braintree**: Primary payment gateway with sandbox/production environment support
- **Authorize.Net**: Secondary payment gateway with merchant authentication
- **Stripe**: Tertiary payment gateway with webhook event subscriptions
- **PayPal**: Payment processing with REST API integration

### Third-Party Services
- **Sift Science**: Third-party fraud detection service for enhanced risk scoring
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
- Manual review threshold: Configurable (default 0.5)
- ONNX model path: `wwwroot/onnx/fraud_model.onnx`
- GeoIP database: External MaxMind database file
- Connection strings and API keys: Managed via Azure Key Vault

## Recent Enhancements (October 2025)

### Repository Pattern & Unit of Work
- **Implementation**: Created repository interfaces (`IRepository<T>`, `IUserProfileRepository`, `ITransactionRepository`, `IFraudAlertRepository`) with Cosmos DB implementations
- **Unit of Work Pattern**: Provides centralized data access with repository aggregation
- **Note**: Cosmos DB limitations - operations execute immediately without traditional ACID transactions across containers. Transaction methods provided for interface compliance only.

### Distributed Tracing
- **Activity Source**: Custom `DistributedTracing` infrastructure using `System.Diagnostics.Activity`
- **Tracing Extensions**: Fraud-specific tags (user_id, transaction_id, fraud_score, decision, risk_level)
- **Event Tracking**: Built-in events for fraud detection, manual review triggers, and alert notifications
- **Performance Monitoring**: Tracks operation duration for fraud scoring pipeline (target: <300ms)

### Idempotency Middleware
- **Purpose**: Prevents duplicate transaction processing for POST/PUT/PATCH requests
- **Implementation**: Memory cache-based with Idempotency-Key header support
- **Features**:
  - Short-circuits duplicate requests before business logic execution
  - Concurrent request detection with 409 Conflict response
  - 24-hour cache retention for idempotency keys
  - X-Idempotency-Replay header on cached responses

### Enhanced Fraud Detection

#### Advanced Risk Scoring Service
- **Device Risk**: Fraud rate analysis, multi-user device detection
- **Velocity Risk**: Transaction volume and frequency analysis (1h, 24h windows)
- **Geolocation Risk**: IP location changes, high-risk country detection, impossible travel detection
- **Amount Risk**: Z-score analysis against user's historical transaction patterns
- **Time-Based Risk**: Unusual hour detection, hourly pattern analysis

#### Ensemble Model Service
- **Multi-Model Approach**: Combines 4 models with weighted averaging
  - ONNX Model (40% weight)
  - Rule-Based Model (25% weight)
  - Statistical Model (20% weight)
  - Behavioral Model (15% weight)
- **Fallback Handling**: Graceful degradation when individual models fail

#### Fraud Rules Engine
- **Blocking Rules**: Blocked countries, blacklisted users, duplicate transactions
- **Review Rules**: High amounts, velocity violations, suspicious patterns, multiple countries
- **Configurable Thresholds**: Amount limits, transaction frequency, country restrictions

### Multi-Gateway Payment Processing with Failover (October 2025)
- **Payment Gateway Service**: Centralized payment processing with automatic failover
  - **Primary Gateway**: Braintree (default)
  - **Failover Order**: Braintree → Authorize.Net → Stripe
  - **Automatic Switching**: System automatically tries next gateway if current gateway fails
  - **Transaction Tracking**: Comprehensive logging of gateway attempts and results
  
- **Gateway Implementations**:
  - Braintree: SDK-based integration with environment configuration (sandbox/production)
  - Authorize.Net: Merchant authentication with transaction processing
  - Stripe: Existing integration maintained as tertiary fallback

**Rationale**: Multi-gateway support with failover prevents payment downtime and provides redundancy. If Braintree experiences issues, the system automatically switches to Authorize.Net, then Stripe if needed.

### Sift Science Fraud Detection Integration (October 2025)
- **Third-Party Fraud Detection**: Sift Science service integration for enhanced fraud scoring
- **Score Blending**: When available, combines ensemble model (70%) with Sift Science score (30%)
- **Fallback Handling**: System uses only ensemble model when Sift Science is unavailable
- **Status Tracking**: Clear status indicators (SUCCESS, NOT_CONFIGURED, SDK_NOT_IMPLEMENTED, ERROR)
- **Current State**: Framework implemented with SDK placeholder; requires real API integration

**Rationale**: Integrating third-party fraud detection provides additional signal for fraud scoring, improving detection accuracy while maintaining system resilience when external services are unavailable.

### Architecture Improvements
- **Interface Segregation**: Separated service interfaces from implementations
- **Dependency Injection**: All new services registered with scoped lifetime
- **Logging & Monitoring**: Comprehensive logging with distributed tracing integration
- **Error Handling**: Graceful error handling with fallback mechanisms in ensemble models