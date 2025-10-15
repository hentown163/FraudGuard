# Azure Functions - Fraud Detection Serverless Implementation

This folder contains serverless Azure Functions for the Global Payment Fraud Detection System, implementing event-driven fraud detection, automated reporting, and real-time analytics.

## Architecture

### Dependency Injection
All functions use proper dependency injection configured in `FunctionsProgram.cs`:
- **Infrastructure**: CosmosClient, ServiceBusClient, SecretClient
- **Repositories**: Transaction, UserProfile, FraudAlert repositories
- **Services**: Fraud detection, behavioral analysis, notifications, ML models

### Configuration
- **host.json**: Azure Functions host configuration with Service Bus settings
- **local.settings.json**: Local development settings (not committed to source control)
- Configuration is loaded from environment variables and appsettings

## HTTP Triggers

### 1. AnalyzeFraudTransaction
**Route**: `POST /api/fraud/analyze`  
**Authorization**: Function level  
**Description**: Real-time fraud analysis for individual transactions

**Request Body**:
```json
{
  "id": "txn_123",
  "userId": "user_456",
  "amount": 1500.00,
  "currency": "USD",
  "gateway": "Stripe",
  "ipAddress": "192.168.1.1",
  "deviceId": "device_789"
}
```

**Response**:
```json
{
  "score": 0.85,
  "decision": "Pending",
  "riskFactors": ["High transaction velocity", "New device"],
  "recommendation": "Manual review required"
}
```

### 2. SearchTransactions
**Route**: `GET /api/fraud/transactions/search`  
**Authorization**: Function level  
**Description**: Search and filter transactions with advanced criteria

**Query Parameters**:
- `userId`: Filter by user ID
- `status`: Filter by status (Approved, Declined, Pending)
- `minScore`: Minimum fraud score threshold
- `startDate`: Start date for range
- `endDate`: End date for range

**Response**:
```json
{
  "count": 42,
  "transactions": [...]
}
```

### 3. GetFraudInsights
**Route**: `GET /api/fraud/insights`  
**Authorization**: Function level  
**Description**: Generate fraud analytics and insights

**Query Parameters**:
- `days`: Number of days to analyze (default: 7)

**Response**:
```json
{
  "period": { "days": 7, "startDate": "...", "endDate": "..." },
  "summary": {
    "totalTransactions": 1500,
    "fraudulentTransactions": 45,
    "fraudRate": 3.0,
    "avgFraudScore": 0.23
  },
  "topRiskFactors": [...],
  "gatewayDistribution": [...]
}
```

## Timer Triggers

### 1. DailyFraudReport
**Schedule**: `0 0 9 * * *` (9 AM UTC daily)  
**Description**: Generates comprehensive daily fraud detection reports

**Features**:
- Transaction volume and fraud rate analysis
- Amount tracking (total and fraud prevented)
- Alert statistics by severity
- Top risk factors identification
- Email delivery to fraud team

### 2. HourlyAnomalyDetection
**Schedule**: `0 0 * * * *` (Every hour)  
**Description**: Detects anomalies in transaction patterns

**Detects**:
- Transaction volume spikes (>3x average)
- Average amount anomalies (>2x historical)
- Fraud rate spikes (>2x historical and >5%)
- Gateway-specific fraud patterns (>30% fraud rate)

**Actions**:
- Creates fraud alerts for detected anomalies
- Sends SMS notifications for critical issues
- Logs anomaly details for investigation

## Service Bus Triggers

### 1. ProcessFraudAlert
**Queue**: `fraud-alerts`  
**Connection**: ServiceBusConnection  
**Description**: Processes fraud alerts from the queue

**Processing**:
1. Deserializes and validates alert message
2. Saves alert to Cosmos DB
3. For Critical/High severity:
   - Sends SMS alert to fraud team
   - Sends detailed email with transaction info
   - Includes risk factors and recommended actions

### 2. BatchProcessTransactions
**Queue**: `transaction-batch`  
**Connection**: ServiceBusConnection  
**Description**: Batch processes multiple transactions for fraud scoring

**Features**:
- Parallel processing for performance
- Thread-safe counters for statistics
- Behavioral analysis for each transaction
- ML model scoring
- Automatic alert generation for high-risk transactions
- Updates transaction status in database

**Performance**: Optimized for high-throughput batch processing

## Deployment

### Local Development
1. Configure `local.settings.json` with your Azure credentials
2. Install Azure Functions Core Tools
3. Run: `func start` or `dotnet run`

### Azure Deployment
1. Create Azure Function App (Isolated Worker, .NET 8)
2. Configure Application Settings:
   - COSMOS_CONNECTION_STRING
   - SERVICEBUS_CONNECTION_STRING
   - KEYVAULT_URI
   - APPLICATIONINSIGHTS_CONNECTION_STRING
3. Deploy using:
   ```bash
   func azure functionapp publish <function-app-name>
   ```

## Monitoring

### Application Insights
- All functions use Application Insights telemetry
- Custom metrics for fraud detection performance
- Distributed tracing enabled
- Sampling configured for cost optimization

### Logging
- Structured logging with ILogger
- Request/response tracking
- Error tracking with stack traces
- Performance metrics

## Security

### Authentication
- Function-level authorization keys
- Azure AD integration supported
- Key Vault for secrets management

### Best Practices
- Secrets stored in Azure Key Vault
- Managed Identity for Azure resource access
- Connection strings via environment variables
- No sensitive data in logs

## Dependencies

### Required Packages
- Microsoft.Azure.Functions.Worker (1.23.0)
- Microsoft.Azure.Functions.Worker.Extensions.Http (3.2.0)
- Microsoft.Azure.Functions.Worker.Extensions.Timer (4.3.1)
- Microsoft.Azure.Functions.Worker.Extensions.ServiceBus (5.22.0)
- Microsoft.Azure.Cosmos (3.54.0)
- Azure.Messaging.ServiceBus (7.18.2)
- Azure.Security.KeyVault.Secrets (4.7.0)

### Shared Services
All functions share services from the main application:
- Fraud scoring and behavioral analysis
- ONNX ML model inference
- Notification services (SMS, Email)
- Repository pattern for data access

## Performance Tuning

### Service Bus Configuration
- Prefetch count: 100 messages
- Max concurrent calls: 32
- Auto-complete enabled
- Max auto-renew duration: 5 minutes

### HTTP Configuration
- Max concurrent requests: 100
- Max outstanding requests: 200
- Custom route prefix: api

### Function Timeout
- Default: 10 minutes
- Configurable per function

## Error Handling

All functions implement:
- Try-catch blocks for error handling
- Detailed error logging
- Graceful degradation
- Retry policies (via Service Bus)
- Dead letter queue support

## Testing

### Unit Testing
Test individual functions with mocked dependencies:
```csharp
var mockFraudService = new Mock<IFraudScoringService>();
var function = new AnalyzeFraudTransactionFunction(logger, mockFraudService.Object, ...);
```

### Integration Testing
Use local emulators:
- Azurite for Storage
- Cosmos DB Emulator
- Service Bus Emulator (if available)

## Monitoring & Alerts

### Key Metrics
- Function execution count
- Average execution time
- Error rate
- Queue length (Service Bus)
- Processing throughput

### Recommended Alerts
- Function failures (>5 in 5 minutes)
- High latency (>5 seconds avg)
- Queue backlog (>1000 messages)
- Anomaly detection triggered

## Future Enhancements

1. **Durable Functions**: For complex workflows and orchestrations
2. **Event Grid Integration**: For event-driven architecture
3. **Blob Triggers**: For bulk data processing
4. **Custom Bindings**: For specialized integrations
5. **GraphQL Support**: For flexible query capabilities
