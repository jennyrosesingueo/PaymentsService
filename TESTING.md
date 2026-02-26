# Testing Guide (Postman)

## Overview

This guide explains how to test the Payments Service using **Postman** — a free tool that lets you
send requests to the service and see what it sends back, without writing any code.

Every test in this guide follows the same idea: you fill in a form in Postman, press Send, and check
whether the response matches what you expect.

---

## Before You Start

### What you need
- **Postman** installed on your computer. Download it free from [postman.com/downloads](https://www.postman.com/downloads/).
- The Payments Service running locally. Ask a developer to start it, or run it yourself with `dotnet run` from the `src/PaymentsService.API` folder.
- Once running, the service is available at: `http://localhost:5008`

---

## Step 1 — Log In and Get a Token

Every request to the Payments Service requires you to log in first. Postman does this in one extra
step before you start testing payments.

Think of the token like a visitor badge — you get it at the front desk, and you carry it with you
for every request you make.

### How to log in

1. Open Postman and click **New** → **Request**.
2. Set the request type to **POST** (the dropdown on the left of the address bar).
3. Enter this address:
   ```
   http://localhost:5008/api/auth/token
   ```
4. Click the **Body** tab, select **raw**, and change the format dropdown from **Text** to **JSON**.
5. Paste the following into the body area:
   ```json
   {
     "clientId": "dev-client",
     "clientSecret": "dev-secret-replace-via-env-in-production"
   }
   ```
6. Click **Send**.

### What a successful login looks like

You should see a response like this:
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAt": "2026-02-27T10:00:00Z"
}
```

Copy the long string next to `"token"`. You will paste it into every payment request you make.

> If you see **401 Unauthorized**, the credentials are wrong. Ask the developer for the correct values.

---

## Step 2 — Add the Token to Your Requests

Every payment request needs to carry the token you just copied.

1. In your new request, click the **Authorization** tab.
2. Change the **Type** dropdown to **Bearer Token**.
3. Paste the token into the **Token** field.

You only need to do this once per request. If the token expires (after 60 minutes), repeat Step 1
to get a new one.

---

## Test Scenarios

### Test 1 — Submit a normal payment

**What you are checking:** A regular payment in a supported currency should be accepted.

1. Create a new **POST** request to:
   ```
   http://localhost:5008/api/payments
   ```
2. Add your token (Authorization → Bearer Token).
3. In the **Body** tab (raw → JSON), paste:
   ```json
   {
     "referenceId": "test-001",
     "amount": 100.00,
     "currency": "USD"
   }
   ```
4. Click **Send**.

**Expected result:** Status `201 Created` and a response body like:
```json
{
  "id": "...",
  "referenceId": "test-001",
  "amount": 100.00,
  "currency": "USD",
  "status": "Completed",
  "failureReason": null
}
```

---

### Test 2 — Submit a large payment

**What you are checking:** A payment over $50,000 should be flagged for manual review, not completed immediately.

1. Same setup as Test 1, but change the body to:
   ```json
   {
     "referenceId": "test-large-001",
     "amount": 100000.00,
     "currency": "USD"
   }
   ```
2. Click **Send**.

**Expected result:** Status `201 Created` and `"status": "Processing"` in the response.

---

### Test 3 — Submit a payment in a blocked currency

**What you are checking:** The currency XTS is permanently blocked. Any payment in XTS should be turned down with a reason.

1. Same setup as Test 1, but change the body to:
   ```json
   {
     "referenceId": "test-xts-001",
     "amount": 50.00,
     "currency": "XTS"
   }
   ```
2. Click **Send**.

**Expected result:** Status `201 Created` and `"status": "Rejected"` in the response, with a `failureReason` that mentions XTS.

---

### Test 4 — Submit the same payment twice

**What you are checking:** If you send the exact same reference number a second time (even with different values), the service should return the original result and not charge twice.

1. First, send the body below and note the response:
   ```json
   {
     "referenceId": "test-duplicate-001",
     "amount": 250.00,
     "currency": "EUR"
   }
   ```
2. Without changing anything, click **Send** a second time with the exact same body.

**Expected result on the second send:**
- Status `200 OK` (not 201 — this signals it was already processed).
- The response matches the **first** response exactly — same amount, same currency, same status.

> Try sending it again with a different amount (e.g. `999.00`) and a different currency (e.g. `GBP`). The result should still show the original €250 EUR — the new values are ignored.

---

### Test 5 — Look up a payment

**What you are checking:** You can retrieve the details of a payment you already submitted using its reference number.

1. Create a new **GET** request to:
   ```
   http://localhost:5008/api/payments/test-001
   ```
   (Replace `test-001` with the reference number of a payment you already submitted in Test 1.)
2. Add your token (Authorization → Bearer Token).
3. Click **Send**.

**Expected result:** Status `200 OK` and the payment details in the response body.

---

### Test 6 — Look up a payment that does not exist

**What you are checking:** Looking up a reference number that was never submitted should return a clear "not found" message, not an error.

1. Create a new **GET** request to:
   ```
   http://localhost:5008/api/payments/does-not-exist
   ```
2. Add your token.
3. Click **Send**.

**Expected result:** Status `404 Not Found` and a response like:
```json
{
  "code": "PAYMENT_NOT_FOUND",
  "message": "No payment found with ReferenceId 'does-not-exist'."
}
```

---

### Test 7 — Send a request without logging in

**What you are checking:** The service should refuse any request that does not carry a valid token.

1. Create a new **POST** request to `http://localhost:5008/api/payments`.
2. Skip the Authorization step — do not add a token.
3. Add any valid JSON body and click **Send**.

**Expected result:** Status `401 Unauthorized`. The service returns nothing else.

---

### Test 8 — Send a payment with missing fields

**What you are checking:** The service should reject a payment that is missing required information and tell you what is wrong.

1. Create a new **POST** request to `http://localhost:5008/api/payments` with your token.
2. In the body, leave out the `currency` field:
   ```json
   {
     "referenceId": "test-bad-001",
     "amount": 100.00
   }
   ```
3. Click **Send**.

**Expected result:** Status `400 Bad Request` and a response that describes what is missing.

---

## Quick Reference

| What you want to do | Method | Address |
|---|---|---|
| Log in and get a token | POST | `http://localhost:5008/api/auth/token` |
| Submit a new payment | POST | `http://localhost:5008/api/payments` |
| Look up an existing payment | GET | `http://localhost:5008/api/payments/{referenceId}` |

Replace `{referenceId}` with the actual reference number, for example:
`http://localhost:5008/api/payments/test-001`

---

## Response Status Summary

| Status shown in Postman | What it means |
|---|---|
| 200 OK | Request succeeded. For payments, this means the reference number was already processed before. |
| 201 Created | A brand-new payment was accepted and saved. |
| 400 Bad Request | Something was missing or wrong in what you sent. The response body explains what. |
| 401 Unauthorized | You did not include a token, or the token has expired. Log in again. |
| 404 Not Found | The reference number you looked up does not exist. |
```
