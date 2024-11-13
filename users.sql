CREATE TABLE "Users" (
    "UserId" SERIAL PRIMARY KEY,
    "Name" TEXT,
    "StudentNumber" TEXT,
    "Course" TEXT,
    "Email" TEXT,
    "Password" TEXT,
    "rfid" VARCHAR(255),
    "role" VARCHAR(50),
    "DateIssued" DATE,
    "ContactNumber" VARCHAR(15)
);
