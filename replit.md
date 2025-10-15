# Global Payment Fraud Detection System

## Overview
This project is an enterprise-grade payment fraud detection system designed for real-time transaction monitoring. It leverages ML-powered risk scoring and provides a comprehensive admin dashboard. The system aims to provide advanced analytics, reporting, and efficient fraud management for various payment gateways, ultimately securing transactions and minimizing financial losses.

## User Preferences
I prefer iterative development, with a focus on understanding the current task before implementation. Please provide detailed explanations when new concepts or significant changes are introduced. I value clear, concise communication and prefer that you ask before making major architectural changes or introducing new dependencies.

## System Architecture
The system is built with ASP.NET Core 8.0 and Razor Pages, following a clean architecture with distinct layers for business logic, data access, and presentation.

**UI/UX Decisions:**
- Professional design system with custom CSS, modern color scheme, gradients, shadows, and animations.
- Full dark mode support with persistent user preference.
- Responsive, mobile-first design with adaptive layouts.
- Clean navigation with breadcrumbs and icons.
- Smooth loading states, transitions, and fade-in animations.

**Technical Implementations:**
- **Frontend:** Bootstrap 5 with a custom professional theme, Chart.js for data visualization, Bootstrap Icons, Inter font family, SignalR for real-time updates, and jsPDF for PDF generation.
- **Backend:** ASP.NET Core 8.0, Razor Pages, FluentValidation for transaction rules, Azure Application Insights for logging, ONNX ML models for fraud detection, and integration with various payment gateways (Stripe, PayPal, Braintree, Authorize.Net).
- **Core Architecture:** Clean architecture with layers for Core (business logic, services, repositories), Controllers (API), Hubs (SignalR), Infrastructure (services, Unit of Work), Middleware (Rate Limiting, Request Logging, Exception Handling), Models (data models, DTOs), Pages (Razor Pages), and Validators.
- **Azure Functions:** Implemented with .NET 8 isolated worker model for scalable, event-driven microservices. Includes HTTP triggers (AnalyzeFraud, BulkAnalyze), Timer Triggers (GenerateDailyFraudReport, HourlyAnomalyDetection), and Service Bus Triggers (ProcessFraudAlert, ProcessBatchTransactions).
- **Custom Middlewares:** ExceptionHandlingMiddleware, RequestLoggingMiddleware, and RateLimitingMiddleware for production readiness.
- **AI Integration:** Includes Azure AI Search for intelligent transaction search, Azure OpenAI for fraud pattern analysis and investigation, Azure Anomaly Detector for time-series anomaly detection, and Azure Text Analytics for sentiment analysis and key phrase extraction.

**Key Features:**
- Real-time fraud detection with ML models and behavioral analytics.
- Multi-gateway payment support.
- Comprehensive admin dashboard with real-time monitoring, advanced filtering, and search.
- Advanced analytics with transaction trends, gateway distribution, and ML model performance metrics.
- Bulk alert management with sorting, filtering, and bulk resolution.
- Export functionality to CSV and PDF.
- Configurable alert rules, email/SMS notifications (Twilio), and webhook configuration.
- Geolocation risk assessment (MaxMind GeoIP2).
- API key management and general preferences.

## External Dependencies
- **Cloud Services:** Azure Cosmos DB, Azure Event Hubs, Azure Service Bus, Azure Key Vault, Azure Application Insights.
- **Payment Gateways:** Stripe, PayPal, Braintree, Authorize.Net.
- **Messaging:** Twilio (SMS), Email services.
- **Geolocation:** MaxMind GeoIP2.
- **Charting:** Chart.js.
- **PDF Generation:** jsPDF, jsPDF-AutoTable.
- **Azure AI Services:** Azure AI Search, Azure OpenAI, Azure Anomaly Detector, Azure Text Analytics.