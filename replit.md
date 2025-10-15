# Global Payment Fraud Detection System

## Project Overview
Enterprise-grade payment fraud detection system with real-time transaction monitoring, ML-powered risk scoring, and comprehensive admin dashboard built with ASP.NET Core 8.0 and Razor Pages.

## Recent Enhancements (October 2025)

### UI/UX Improvements
- **Professional Design System**: Custom CSS with modern color scheme, gradients, shadows, and animations
- **Dark Mode Support**: Full dark theme with persistent user preference
- **Responsive Design**: Mobile-first approach with adaptive layouts
- **Enhanced Navigation**: Clean navigation with breadcrumbs and icons
- **Loading States**: Smooth transitions and fade-in animations throughout

### Feature Enhancements

#### Dashboard
- Real-time transaction monitoring with SignalR
- Advanced filtering by status (Approved, Declined, Pending, High Risk)
- Search functionality across transaction ID, user, and amount
- Date range filtering
- Live-updating charts (Fraud Score Trend & Risk Distribution)
- Export to CSV and PDF with full transaction data
- Animated stat cards with gradient backgrounds

#### Analytics Page
- Transaction volume trends visualization
- Gateway distribution pie chart
- ML model performance metrics (Precision, Recall, F1 Score, AUC-ROC)
- Geographic fraud heatmap placeholder
- Top fraud indicators with impact scores
- Comprehensive business intelligence metrics

#### Settings Page
- Email notification preferences
- SMS alerts via Twilio integration
- Custom alert rules management
- Webhook configuration
- API key management
- General preferences (timezone, date format, currency)
- Tabbed interface for easy navigation

#### Alerts Page
- Bulk alert management with checkboxes
- Filtering by severity, status, and type
- Priority-based sorting
- Bulk resolve functionality
- Real-time alert statistics cards
- Interactive alert investigation workflow

### Technical Implementation

#### Frontend
- **CSS Framework**: Bootstrap 5 + Custom Professional Theme
- **Charts**: Chart.js for data visualization
- **Icons**: Bootstrap Icons
- **Fonts**: Inter font family for modern typography
- **Real-time**: SignalR for live updates
- **Export**: jsPDF + jsPDF-AutoTable for PDF generation

#### Backend
- **Framework**: ASP.NET Core 8.0
- **Architecture**: Razor Pages with clean separation
- **Validation**: FluentValidation with comprehensive transaction rules
- **Logging**: Azure Application Insights for telemetry and monitoring
- **Payment Gateways**: Stripe, PayPal, Braintree, Authorize.Net
- **Fraud Detection**: ONNX ML models, behavioral analytics
- **Notifications**: Twilio (SMS), Email
- **Cloud Services**: Azure Cosmos DB, Event Hubs, Service Bus, Key Vault
- **Geolocation**: MaxMind GeoIP2

### Key Files Modified
- `wwwroot/css/professional-theme.css` - Complete design system
- `wwwroot/js/site.js` - Dark mode, toast notifications, utilities
- `Pages/Shared/_Layout.cshtml` - Enhanced navigation and dark mode toggle
- `Pages/Dashboard/Index.cshtml` - Full-featured dashboard with filtering and export
- `Pages/Analytics/Index.cshtml` - New analytics and insights page
- `Pages/Settings/Index.cshtml` - New settings management page
- `Pages/Alerts/Index.cshtml` - Enhanced alerts with bulk actions

### User Experience Features
- **Dark Mode Toggle**: Floating button with icon animation
- **Toast Notifications**: Non-intrusive success/error messages
- **Search & Filter**: Real-time client-side filtering
- **Export Options**: Professional CSV and PDF reports
- **Responsive Tables**: Horizontal scrolling on mobile
- **Loading Indicators**: Skeleton screens and spinners
- **Smooth Animations**: Fade-in, slide-in, hover effects

### Browser Compatibility
- Chrome 90+
- Firefox 88+
- Safari 14+
- Edge 90+

### Performance Optimizations
- CSS custom properties for theme switching
- Efficient event listeners with delegation
- Chart data limiting (20 points max for line charts)
- Lazy loading for heavy components
- Optimized asset delivery via CDN

## Development Setup
1. Ensure .NET 8.0 SDK is installed
2. Run `dotnet restore` to install dependencies
3. Configure Azure services and API keys in `appsettings.json`
4. Run `dotnet run` to start the development server
5. Access the application at `http://localhost:5000`

## Environment Variables
- `APPLICATIONINSIGHTS_CONNECTION_STRING` - Azure Application Insights connection string
- Azure Cosmos DB connection string
- Event Hubs connection string
- Service Bus connection string
- Key Vault URI
- Twilio credentials
- Stripe/PayPal API keys

## Project Structure (Reorganized - October 2025)

### Clean Architecture Layers
- `/Core` - Business logic layer with proper separation of concerns
  - `/Core/Interfaces/Services` - Service interface definitions
  - `/Core/Interfaces/Repositories` - Repository interface definitions
  - `/Core/Services` - Service implementations (fraud detection, payment gateways, notifications)
  - `/Core/Repositories` - Data access layer implementations
- `/Controllers` - API controllers
- `/Hubs` - SignalR hubs for real-time communication
- `/Infrastructure` - Core infrastructure services and Unit of Work pattern
- `/Middleware` - Custom middleware components (Rate Limiting, Request Logging, Exception Handling)
- `/Models` - Data models and DTOs
- `/Pages` - Razor Pages (Dashboard, Analytics, Settings, Alerts, Transactions)
- `/Validators` - FluentValidation validators for data integrity
- `/wwwroot` - Static assets (CSS, JS, images)

### Custom Middlewares (Production-Ready)
1. **ExceptionHandlingMiddleware** - Global error handling with environment-aware diagnostics
2. **RequestLoggingMiddleware** - Request/response logging with unique request IDs and performance tracking
3. **RateLimitingMiddleware** - Thread-safe rate limiting with sliding window (100 req/min per IP/endpoint)

## Features Summary
✅ Real-time fraud detection with ML models
✅ Multi-gateway payment support
✅ Comprehensive admin dashboard
✅ Advanced analytics and reporting
✅ Bulk alert management
✅ CSV/PDF export functionality
✅ Dark mode support
✅ Responsive mobile design
✅ Professional UI/UX
✅ Real-time notifications
✅ Behavioral analytics
✅ Geolocation risk assessment
✅ Configurable alert rules
✅ Webhook integrations
✅ FluentValidation with date/time checks
✅ Azure Application Insights telemetry

## Azure Functions - Serverless Fraud Detection (October 2025 - ✅ Implemented)

### Azure Functions - Production-Ready Microservices
A comprehensive Azure Functions implementation built with .NET 8 isolated worker model, providing scalable event-driven microservices for fraud detection operations.

**Location:** `/GlobalPaymentFraudDetection/Functions/`

**Architecture Overview:**
- ✅ .NET 8 Isolated Worker Model (future-proof, in-process deprecated Nov 2026)
- ✅ Dependency Injection with ASP.NET Core patterns
- ✅ Application Insights telemetry and monitoring
- ✅ Managed Identity support for Azure services
- ✅ Production-ready error handling and logging

### Implemented Functions

#### HTTP Triggers (Real-time APIs)
1. **AnalyzeFraud** - `POST /api/fraud/analyze`
   - Single transaction fraud analysis
   - Returns fraud score, risk level, decision, and risk factors
   - Authorization: Function level (requires API key)

2. **BulkAnalyze** - `POST /api/fraud/bulk-analyze`
   - Batch analysis for multiple transactions
   - Optimized for high-throughput scenarios
   - Async processing with parallel analysis

#### Timer Triggers (Scheduled Operations)
3. **GenerateDailyFraudReport** - Daily at 2:00 AM UTC
   - Comprehensive daily fraud statistics
   - Transaction volume and fraud rate calculations
   - Top risk factors and gateway distribution
   - Automated report notifications

4. **HourlyAnomalyDetection** - Every hour
   - Real-time anomaly detection in transaction patterns
   - Statistical and behavioral analysis
   - Proactive alert generation

#### Service Bus Triggers (Event-Driven Processing)
5. **ProcessFraudAlert** - Queue: `fraud-alerts`
   - Asynchronous alert processing by severity
   - Critical/High alerts trigger immediate notifications
   - Medium/Low alerts queued for review
   - Persistent storage in Cosmos DB

6. **ProcessBatchTransactions** - Queue: `transaction-batch`
   - Bulk transaction processing from queue
   - Parallel analysis for efficiency
   - High-risk percentage monitoring

### Technology Stack
- **Runtime**: .NET 8.0 Isolated Worker
- **Functions Version**: v4
- **Packages**:
  - Microsoft.Azure.Functions.Worker 2.0.0
  - Microsoft.Azure.Functions.Worker.Sdk 2.0.0
  - Azure.Messaging.ServiceBus 7.18.2
  - Microsoft.Azure.Cosmos 3.54.0
  - Azure.Security.KeyVault.Secrets 4.7.0
  - Application Insights integration

### Configuration Files
- `Program.cs` - Dependency injection and service configuration
- `host.json` - Function host settings and logging configuration
- `local.settings.json` - Local development environment variables
- `Functions.csproj` - Project dependencies and build settings
- `README.md` - Comprehensive documentation

### Local Development
```bash
cd GlobalPaymentFraudDetection/Functions
dotnet build
func start
```

### Deployment
```bash
# Azure CLI deployment
func azure functionapp publish <FunctionAppName>

# Required app settings:
# - FUNCTIONS_WORKER_RUNTIME=dotnet-isolated
# - CosmosDbConnectionString
# - ServiceBusConnectionString
# - KeyVaultUri
# - APPLICATIONINSIGHTS_CONNECTION_STRING
```

### Security Features
- Function-level authorization with API keys
- Azure Managed Identity for service access
- Key Vault integration for secrets management
- Secure connection string management

### Performance Optimizations
- Singleton registration for HTTP/DB clients (connection pooling)
- Async/await throughout for non-blocking I/O
- Cancellation token support for graceful shutdowns
- Service Bus batch processing with configurable concurrency

### Monitoring & Observability
- Application Insights structured logging
- Request/dependency tracking
- Custom metrics and telemetry
- Exception tracking with stack traces
- Performance monitoring and alerts

### Azure AI Services Integration

**Azure AI Search:**
- Intelligent transaction search with natural language queries
- Semantic search capabilities for complex fraud pattern detection
- Automatic indexing of transactions for fast retrieval
- Full-text search with filters and facets

**Azure OpenAI:**
- AI-powered fraud pattern analysis and insights
- Natural language fraud investigation assistant
- Automated fraud summary generation
- Anomaly detection with AI explanations
- Conversational fraud Q&A interface

**Azure Anomaly Detector:**
- Time-series anomaly detection for transaction patterns
- Statistical and AI-based anomaly identification
- Real-time transaction anomaly scoring
- Historical pattern analysis

**Azure Text Analytics:**
- Sentiment analysis for transaction descriptions
- Key phrase extraction from fraud alerts
- Entity recognition in transaction metadata

### AI Assistant UI (Temporarily Disabled)
A comprehensive AI-powered fraud investigation interface has been built:
- Natural language chat interface for fraud questions
- Intelligent transaction search with semantic understanding
- Quick-access fraud pattern queries
- Real-time AI insights and recommendations

**Location:** `/tmp/AIAssistant.backup` - Available for activation once Azure credentials are configured

### Configuration Required
To activate Azure AI features, add the following to `appsettings.json`:

```json
{
  "AzureAISearch": {
    "Endpoint": "https://your-search-service.search.windows.net",
    "ApiKey": "your-api-key"
  },
  "AzureOpenAI": {
    "Endpoint": "https://your-openai-service.openai.azure.com",
    "ApiKey": "your-api-key",
    "DeploymentName": "gpt-4"
  },
  "AzureAnomalyDetector": {
    "Endpoint": "https://your-anomaly-detector.cognitiveservices.azure.com",
    "ApiKey": "your-api-key"
  }
}
```

Then:
1. Uncomment Azure package references in `GlobalPaymentFraudDetection.csproj`
2. Uncomment Azure service registrations in `Program.cs`
3. Move backup files from `/tmp` back to project
4. Rebuild and redeploy

### Future Enhancements
- Geographic map visualization with real coordinates
- Advanced ML model retraining interface
- User role management and permissions
- Multi-language support
- Advanced reporting with custom date ranges
- Integration with more payment gateways
- Enhanced behavioral pattern detection
- Automated response actions based on rules
- **Azure Foundry AI Studio integration** for model fine-tuning
- **Multi-modal AI analysis** (OCR for document fraud detection)
- **Predictive fraud forecasting** using Azure Machine Learning
