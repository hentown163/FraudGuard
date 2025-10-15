# FluentValidation and Application Insights Testing Guide

## Transaction Validation Rules

The TransactionValidator enforces the following rules:

### Date Validation (Real-Time Transaction Stream)
- **Past Date Check**: Transaction timestamp cannot be older than 5 minutes from current time
- **Future Date Check**: Transaction timestamp cannot be more than 1 minute ahead of current time

### Other Validation Rules
1. **Transaction ID**: Required, 1-100 characters
2. **User ID**: Required, 1-100 characters
3. **Amount**: Must be > 0 and ≤ 1,000,000
4. **Currency**: Required, must be valid 3-letter ISO code (USD, EUR, GBP, JPY, CAD, AUD, CHF, CNY, INR)
5. **Timestamp**: Required, must pass date validation
6. **IP Address**: Required, must be valid format
7. **Payment Gateway**: Required, must be one of: Stripe, PayPal, Braintree, Authorize.Net
8. **Payment Method**: Required
9. **Country**: Required, 2-letter ISO code

## Testing Validation

### Valid Transaction Example
```json
POST /api/frauddetection/score
{
  "transactionId": "tx_123456789",
  "userId": "user_987654321",
  "amount": 100.50,
  "currency": "USD",
  "timestamp": "2025-10-15T07:20:00Z",
  "ipAddress": "192.168.1.1",
  "paymentGateway": "Stripe",
  "paymentMethod": "credit_card",
  "country": "US"
}
```

### Invalid Transaction Examples

#### Past Date Error (>5 minutes old)
```json
{
  "timestamp": "2025-10-15T06:00:00Z"
}
```
Error: "Transaction timestamp cannot be in the past (older than 5 minutes)"

#### Future Date Error (>1 minute ahead)
```json
{
  "timestamp": "2025-10-15T08:00:00Z"
}
```
Error: "Transaction timestamp cannot be in the future (more than 1 minute ahead)"

#### Invalid Currency
```json
{
  "currency": "XXX"
}
```
Error: "Currency must be a valid ISO currency code"

#### Invalid Amount
```json
{
  "amount": -50
}
```
Error: "Amount must be greater than 0"

## Application Insights Configuration

### Setup
1. Create an Application Insights resource in Azure Portal
2. Copy the Connection String
3. Add to environment variables:
   ```bash
   export APPLICATIONINSIGHTS_CONNECTION_STRING="InstrumentationKey=...;IngestionEndpoint=..."
   ```
   OR add to appsettings.json:
   ```json
   {
     "ApplicationInsights": {
       "ConnectionString": "InstrumentationKey=...;IngestionEndpoint=..."
     }
   }
   ```

### What Gets Logged
- All HTTP requests/responses (via RequestLoggingMiddleware)
- Exceptions and errors
- Custom telemetry events
- Performance metrics
- Dependencies (Cosmos DB, Service Bus, etc.)

### View Logs in Azure Portal
1. Navigate to Application Insights resource
2. Click "Logs" in left menu
3. Run KQL queries:
   ```kusto
   traces
   | where timestamp > ago(1h)
   | order by timestamp desc
   ```

### Local Testing (Without Azure)
The application works without Application Insights connection string. Logs will appear in console only.
